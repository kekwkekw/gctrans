#nullable enable
using System.Text.Json;
using System.Runtime.InteropServices;
namespace GCMod.Nextgen;

public sealed record StoredPackage(string ContentId, string Root, TranslationPackageSnapshot Snapshot);
public sealed record PendingRestartSelection(int Schema, string ContentId, string? PreviousContentId, string Reason);
/// Only complete validated generations are selectable. Repairs replace directories, never shared bytes.
public sealed class LocalImmutablePackageStore(string root, PackageLoader loader)
{
    public string Root { get; } = Path.GetFullPath(root);
    readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> validated = new(StringComparer.Ordinal);
    readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> inflight = new(StringComparer.Ordinal);
    readonly SemaphoreSlim writer = new(1);
    sealed record Selection(int Schema, string ContentId);
    static bool IsId(string? id) => id?.Length == 64 && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    string PackagePath(string id) { ContractRules.Require(IsId(id), "INVALID_CONTENT_ID"); return Path.Combine(Root, "packages", id); }
    static bool Recoverable(Exception ex) => ex is IOException or JsonException or UnauthorizedAccessException or PackageValidationException or InvalidOperationException or KeyNotFoundException or FormatException;
    Selection? ReadSelection(string name)
    {
        try { var selection = JsonSerializer.Deserialize<Selection>(File.ReadAllBytes(Path.Combine(Root, name))); return selection is { Schema: 1 } && IsId(selection.ContentId) ? selection : null; }
        catch (Exception ex) when (Recoverable(ex)) { return null; }
    }
    public PendingRestartSelection? ReadPendingRestart()
    {
        try { var row = JsonSerializer.Deserialize<PendingRestartSelection>(File.ReadAllBytes(Path.Combine(Root, "pending_restart.json"))); return row is { Schema: 1 } && IsId(row.ContentId) ? row : null; }
        catch (Exception ex) when (Recoverable(ex)) { return null; }
    }
    async Task<StoredPackage?> LoadStoredAsync(string identity, CancellationToken ct, TranslationPackageSnapshot? previous = null)
    {
        try
        {
            var path = PackagePath(identity);
            if (Directory.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new PackageValidationException("OWNED_PACKAGE_ROOT_LINK_NOT_ALLOWED");
            if (!File.Exists(Path.Combine(path, "complete.json"))) { validated.TryRemove(identity, out _); return null; }
            var snapshot = await loader.LoadAsync(path, previous: previous, cancellationToken: ct).ConfigureAwait(false);
            if (snapshot.Identity != identity) { validated.TryRemove(identity, out _); return null; }
            // Complete is a file-set promise even though Novel body integrity stays lazy.
            // Metadata consistency never promotes untrusted bytes to a verified content hash.
            foreach (var descriptor in snapshot.Payloads.Descriptors.Values)
                if (!TryPlainStoredFile(path, descriptor, out _)) throw new PackageValidationException("STORED_COMPLETE_PAYLOAD_MISSING_OR_TRUNCATED");
            validated[identity] = 0; return new(identity, path, snapshot);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (Recoverable(ex)) { validated.TryRemove(identity, out _); return null; }
    }
    public async Task<StoredPackage?> RecoverAsync(CancellationToken cancellationToken = default)
    {
        foreach (var name in new[] { "current.json", "previous.json" })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selection = ReadSelection(name); if (selection == null) continue;
            var package = await LoadStoredAsync(selection.ContentId, cancellationToken).ConfigureAwait(false);
            if (package != null) return package;
        }
        return null;
    }
    // Existing immutable custody/loader rules, without re-import or mutation of selection records.
    public Task<StoredPackage?> LoadContentAsync(string contentId, TranslationPackageSnapshot? previous = null, CancellationToken cancellationToken = default)
    {
        ContractRules.Require(IsId(contentId), "INVALID_CONTENT_ID");
        return LoadStoredAsync(contentId, cancellationToken, previous);
    }
    public async Task<StoredPackage?> RecoverPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = ReadPendingRestart();
        return pending == null ? null : await LoadStoredAsync(pending.ContentId, cancellationToken).ConfigureAwait(false);
    }
    bool Protected(string id) => inflight.ContainsKey(id) || new[] { "current.json", "previous.json" }.Any(n => ReadSelection(n)?.ContentId == id) || ReadPendingRestart()?.ContentId == id;
    public bool TryGetValidatedReusePath(TranslationPackageSnapshot snapshot, PackageFile descriptor, out string path)
    {
        path = "";
        if (!validated.ContainsKey(snapshot.Identity) || !snapshot.Payloads.Descriptors.TryGetValue(descriptor.Path, out var old) || old != descriptor
            || !string.Equals(Path.GetFullPath(snapshot.Payloads.Root), PackagePath(snapshot.Identity), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            || !File.Exists(Path.Combine(snapshot.Payloads.Root, "complete.json"))) return false;
        return TryPlainStoredFile(snapshot.Payloads.Root, descriptor, out path);
    }
    static bool TryPlainStoredFile(string packageRoot, PackageFile descriptor, out string path)
    {
        path = "";
        var candidatePath = Path.Combine(packageRoot, descriptor.Path.Replace('/', Path.DirectorySeparatorChar));
        // Store custody does not authorize a replaced child/root junction, missing body or truncation.
        try
        {
            if (new FileInfo(candidatePath).Length != descriptor.Size) return false;
            for (string? cursor = candidatePath; cursor != null; cursor = Path.GetDirectoryName(cursor))
            {
                if (File.GetAttributes(cursor).HasFlag(FileAttributes.ReparsePoint)) return false;
                if (string.Equals(cursor, packageRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) { path = candidatePath; return true; }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return false;
    }
    public async Task<StoredPackage> StageAsync(string authoringRoot, TranslationPackageSnapshot? previous = null, CancellationToken cancellationToken = default)
    {
        await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporary = null; string? identity = null;
        try
        {
            var candidate = await loader.LoadAsync(authoringRoot, previous: previous, cancellationToken: cancellationToken).ConfigureAwait(false);
            identity = candidate.Identity; var target = PackagePath(identity); bool repairedCache = false;
            if (Directory.Exists(target))
            {
                var existing = await LoadStoredAsync(identity, cancellationToken, previous).ConfigureAwait(false);
                if (existing != null && !Protected(identity) && previous?.Identity != identity)
                {
                    // Explicit same-ID inactive recovery is a cold repair check, not the ordinary
                    // selected-generation fast path. Verify lazy bodies before adopting that cache.
                    try { foreach (var descriptor in existing.Snapshot.Payloads.Descriptors.Values) _ = await existing.Snapshot.Payloads.ReadAsync(descriptor.Path, cancellationToken).ConfigureAwait(false); }
                    catch (Exception ex) when (Recoverable(ex)) { validated.TryRemove(identity, out _); existing = null; }
                }
                if (existing != null) return existing;
                ContractRules.Require(!Protected(identity) && previous?.Identity != identity, "CORRUPT_PROTECTED_CACHE_RESTART_REQUIRED");
                var quarantine = Path.Combine(Root, "quarantine"); Directory.CreateDirectory(quarantine);
                // Rename severs the bad directory from selection; hardlinked valid bytes are untouched.
                Directory.Move(target, Path.Combine(quarantine, identity + "-" + Guid.NewGuid().ToString("N"))); repairedCache = true;
            }
            inflight[identity] = 0;
            var reuse = repairedCache ? null : previous ?? (await RecoverAsync(cancellationToken).ConfigureAwait(false))?.Snapshot;
            Directory.CreateDirectory(Path.Combine(Root, "packages"));
            temporary = Path.Combine(Root, "packages", ".incomplete-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            foreach (var descriptor in candidate.Payloads.Descriptors.Values.OrderBy(x => x.Path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = Path.Combine(temporary, descriptor.Path.Replace('/', Path.DirectorySeparatorChar));
                var parent = Path.GetDirectoryName(destination); if (parent == null) throw new IOException("STAGING_PARENT_REQUIRED"); Directory.CreateDirectory(parent);
                bool linked = false;
                if (reuse != null && TryGetValidatedReusePath(reuse, descriptor, out var from))
                {
                    linked = OperatingSystem.IsWindows() ? CreateHardLink(destination, from, IntPtr.Zero) : Link(from, destination) == 0;
                    if (!linked) { try { File.Copy(from, destination); linked = true; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
                }
                if (!linked)
                {
                    var bytes = await candidate.Payloads.ReadAsync(descriptor.Path, cancellationToken).ConfigureAwait(false);
                    if (bytes == null) throw new PackageValidationException("STAGING_PAYLOAD_MISSING");
                    await File.WriteAllBytesAsync(destination, bytes, cancellationToken).ConfigureAwait(false);
                }
            }
            var manifest = await new LocalPackageFileSource().ReadAsync(authoringRoot, "ui/manifest.json", cancellationToken).ConfigureAwait(false);
            ContractRules.Require(PackageLoader.Hash(manifest) == candidate.Identity, "STAGING_MANIFEST_CHANGED");
            await File.WriteAllBytesAsync(Path.Combine(temporary, "ui", "manifest.json"), manifest, cancellationToken).ConfigureAwait(false);
            var release = Path.Combine(authoringRoot, "release-manifest.json");
            if (File.Exists(release)) await File.WriteAllBytesAsync(Path.Combine(temporary, "release-manifest.json"), await File.ReadAllBytesAsync(release, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(temporary, "complete.json"), JsonSerializer.Serialize(new Selection(1, identity)), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var ready = await loader.LoadAsync(temporary, previous: previous, cancellationToken: cancellationToken).ConfigureAwait(false);
            ContractRules.Require(ready.Identity == identity, "STAGED_PACKAGE_IDENTITY_MISMATCH");
            Directory.Move(temporary, target); temporary = null;
            var snapshot = await loader.LoadAsync(target, previous: previous, cancellationToken: cancellationToken).ConfigureAwait(false);
            validated[snapshot.Identity] = 0; return new(snapshot.Identity, target, snapshot);
        }
        finally
        {
            // Only this operation's exact random directory is removed. No wildcard/global scavenging.
            if (temporary != null && Path.GetDirectoryName(temporary) == Path.Combine(Root, "packages") && Path.GetFileName(temporary).StartsWith(".incomplete-", StringComparison.Ordinal))
                try { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            if (identity != null) inflight.TryRemove(identity, out _);
            writer.Release();
        }
    }
    void RequireComplete(TranslationPackageSnapshot snapshot)
    {
        ContractRules.Require(string.Equals(Path.GetFullPath(snapshot.Payloads.Root), PackagePath(snapshot.Identity), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            && validated.ContainsKey(snapshot.Identity) && !File.GetAttributes(snapshot.Payloads.Root).HasFlag(FileAttributes.ReparsePoint) && File.Exists(Path.Combine(snapshot.Payloads.Root, "complete.json")), "SELECTION_REQUIRES_COMPLETE_IMMUTABLE_PACKAGE");
    }
    public void RecordPendingRestart(TranslationPackageSnapshot snapshot, string? previousContentId, string reason)
    {
        RequireComplete(snapshot); Directory.CreateDirectory(Root);
        AtomicWrite(Path.Combine(Root, "pending_restart.json"), JsonSerializer.SerializeToUtf8Bytes(new PendingRestartSelection(1, snapshot.Identity, previousContentId, reason)));
    }
    public void CompletePendingRestart(string contentId)
    { if (ReadPendingRestart()?.ContentId == contentId) File.Delete(Path.Combine(Root, "pending_restart.json")); }
    public void RejectPendingRestart(string reason)
    {
        var pending = ReadPendingRestart(); if (pending == null) return;
        AtomicWrite(Path.Combine(Root, "pending_restart_failure.json"), JsonSerializer.SerializeToUtf8Bytes(pending with { Reason = reason }));
        File.Delete(Path.Combine(Root, "pending_restart.json"));
    }
    public void Select(TranslationPackageSnapshot snapshot)
    {
        RequireComplete(snapshot); Directory.CreateDirectory(Root);
        var currentPath = Path.Combine(Root, "current.json"); var old = ReadSelection("current.json");
        // Only this process's validated old current can replace a known previous LKG.
        if (old != null && old.ContentId != snapshot.Identity && validated.ContainsKey(old.ContentId))
            AtomicWrite(Path.Combine(Root, "previous.json"), JsonSerializer.SerializeToUtf8Bytes(old));
        AtomicWrite(currentPath, JsonSerializer.SerializeToUtf8Bytes(new Selection(1, snapshot.Identity)));
    }
    static void AtomicWrite(string path, byte[] bytes)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); } File.Move(temporary, path, true); }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] static extern bool CreateHardLink(string destination, string existing, IntPtr securityAttributes);
    [DllImport("libc", EntryPoint = "link", SetLastError = true)] static extern int Link(string existing, string destination);
}
