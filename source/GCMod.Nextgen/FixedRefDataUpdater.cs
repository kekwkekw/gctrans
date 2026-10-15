#nullable enable
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace GCMod.Nextgen;

public sealed record UpdateSource(string Repository, string Channel, string PackagePath = "translations/ko",
    Uri? ApiBase = null, Uri? ContentBase = null);
public sealed class UpdateMetrics
{
    public int MetadataRequests { get; internal set; }
    public long MetadataBytes { get; internal set; }
    public int PayloadDownloads { get; internal set; }
    public long PayloadBytes { get; internal set; }
    public int ReusedFiles { get; internal set; }
    public int OsLinks { get; internal set; }
    public int OsCopies { get; internal set; }
    public long OsCopyBytes { get; internal set; }
    public int AppPayloadReads { get; internal set; }
    public int AppPayloadHashes { get; internal set; }
    public int Retries { get; internal set; }
    public bool ColdImport { get; internal set; }
}
public sealed record UpdatePreparation(string Reason, string? TransportCommit, PreparedPackage? Prepared, UpdateMetrics Metrics, string? BasedOnIdentity = null);

/// <summary>Worker-only complete JSON package preparation. Selection and native writes belong to RuntimeCoordinator.</summary>
public sealed class FixedRefDataUpdater(HttpClient client, string temporaryRoot, SafeDiagnostics? diagnostics = null)
{
    readonly string ownedRoot = Path.GetFullPath(temporaryRoot);
    readonly SafeDiagnostics log = diagnostics ?? new();
    readonly SemaphoreSlim gate = new(1, 1);
    public static HttpClient CreateClient() => new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false })
        { Timeout = Timeout.InfiniteTimeSpan };

    public async Task<UpdatePreparation> PrepareAsync(UpdateSource source, RuntimeCoordinator coordinator, CancellationToken cancellationToken = default)
    {
        var metrics = new UpdateMetrics();
        TranslationPackageSnapshot? baseline = null;
        string? commit = null, temporary = null;
        bool entered = false;
        try
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            ValidateSource(source);
            ContractRules.Require(coordinator.HasImmutableStore, "UPDATE_IMMUTABLE_STORE_REQUIRED");
            baseline = await coordinator.RecoverPreparationBaselineAsync(cancellationToken).ConfigureAwait(false);
            metrics.ColdImport = baseline == null;
            // v1 intentionally carries no credentials. In particular, a caller's default Authorization cannot follow redirects.
            ContractRules.Require(client.DefaultRequestHeaders.All(header => new[] { "User-Agent", "Accept", "Accept-Encoding" }.Contains(header.Key, StringComparer.OrdinalIgnoreCase)), "UPDATE_CLIENT_CREDENTIALS_UNSUPPORTED");
            var api = source.ApiBase ?? new Uri("https://api.github.com/");
            var content = source.ContentBase ?? new Uri("https://raw.githubusercontent.com/");
            var resolveUri = new Uri(api, "repos/" + source.Repository + "/commits/" + Uri.EscapeDataString(source.Channel));
            var resolved = await FetchAsync(resolveUri, 1024 * 1024, metrics, true, cancellationToken).ConfigureAwait(false);
            using (var node = ContractRules.Parse(resolved)) commit = ContractRules.String(node.RootElement, "sha", 40);
            ContractRules.Require(commit.Length == 40 && commit.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "UPDATE_FIXED_COMMIT_REQUIRED");
            Uri FileUri(string path) => new(content, source.Repository + "/" + commit + "/" + source.PackagePath + "/" + path);
            var releaseBytes = await FetchAsync(FileUri("release-manifest.json"), 8 * 1024 * 1024, metrics, true, cancellationToken).ConfigureAwait(false);
            var release = ReleaseDescriptor.Parse(releaseBytes);
            var uiBytes = await FetchAsync(FileUri(release.UiManifest.Path), release.UiManifest.Size, metrics, true, cancellationToken).ConfigureAwait(false);
            release.VerifyUi(uiBytes);
            // Prove the UI root before materializing any payload. Its closure is validated by PackageLoader after materialization.
            using (var _ = ContractRules.Parse(uiBytes)) { }
            Directory.CreateDirectory(ownedRoot);
            temporary = Path.Combine(ownedRoot, ".download-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            Directory.CreateDirectory(Path.Combine(temporary, "ui"));
            await File.WriteAllBytesAsync(Path.Combine(temporary, "release-manifest.json"), releaseBytes, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(temporary, "ui", "manifest.json"), uiBytes, cancellationToken).ConfigureAwait(false);
            foreach (var file in release.Files.OrderBy(x => x.Path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = Path.Combine(temporary, file.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (baseline != null && coordinator.TryGetValidatedReusePath(baseline, file, out var existing) && Reuse(existing, destination, file, metrics)) continue;
                var bytes = await FetchAsync(FileUri(file.Path), file.Size, metrics, false, cancellationToken).ConfigureAwait(false);
                metrics.AppPayloadReads++; metrics.AppPayloadHashes++;
                ContractRules.Require(bytes.LongLength == file.Size && PackageLoader.Hash(bytes) == file.Sha256, "UPDATE_PAYLOAD_INTEGRITY");
                // Decode/parse the exact verified bytes. No archives, native code, fonts, or arbitrary installer paths are accepted.
                LegacyPayloadContracts.Validate(file, bytes);
                await File.WriteAllBytesAsync(destination, bytes, cancellationToken).ConfigureAwait(false);
            }
            var prepared = await coordinator.PrepareAsync(temporary, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (prepared == null) return new("UPDATE_CANDIDATE_REJECTED_SELECTION_RETAINED", commit, null, metrics);
            log.Record("FIXED_REF_CANDIDATE_PREPARED_NOT_SELECTED");
            return new(prepared.Snapshot.Identity == baseline?.Identity ? "UPDATE_NO_CHANGE" : "UPDATE_PREPARED", commit, prepared, metrics, baseline?.Identity);
        }
        catch (OperationCanceledException) { log.Record("UPDATE_CANCELLED_SELECTION_RETAINED"); return new("UPDATE_CANCELLED", commit, null, metrics); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or PackageValidationException
            or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { log.Record("UPDATE_FAILED_SELECTION_RETAINED"); return new("UPDATE_FAILED_SELECTION_RETAINED", commit, null, metrics); }
        finally
        {
            // The only removable path is this operation's freshly generated direct child; no store current/previous/pending is touched.
            if (temporary != null && Path.GetDirectoryName(temporary) == ownedRoot && Path.GetFileName(temporary).StartsWith(".download-", StringComparison.Ordinal))
                try { Directory.Delete(temporary, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log.Record("UPDATE_OWNED_TEMP_CLEANUP_DEFERRED"); }
            if (entered) gate.Release();
        }
    }
    static void ValidateSource(UpdateSource source)
    {
        var parts = source.Repository.Split('/');
        ContractRules.Require(parts.Length == 2 && parts.All(x => x.Length is > 0 and <= 100 && x.All(c => AsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            && parts.All(x => x is not "." and not ".."), "UPDATE_REPOSITORY");
        ContractRules.Require(source.Channel.Length is > 0 and <= 128 && !source.Channel.Any(char.IsControl)
            && source.Channel.All(c => AsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '/'), "UPDATE_CHANNEL");
        ContractRules.SafePath(source.PackagePath);
        ContractRules.Require(source.PackagePath.Split('/').All(part=>part.All(c=>AsciiLetterOrDigit(c)||c is '-' or '_' or '.')), "UPDATE_PACKAGE_URL_PATH");
        foreach (var uri in new[] { source.ApiBase, source.ContentBase }.Where(x => x != null))
            ContractRules.Require(uri!.IsAbsoluteUri && uri.Scheme == "https" && uri.UserInfo.Length == 0 && uri.Query.Length == 0
                && uri.Fragment.Length == 0 && uri.AbsolutePath.EndsWith('/'), "UPDATE_TRUSTED_HTTPS_BASE_REQUIRED");
    }
    static bool AsciiLetterOrDigit(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
    async Task<byte[]> FetchAsync(Uri uri, long bound, UpdateMetrics metrics, bool metadata, CancellationToken cancellationToken)
    {
        ContractRules.Require(bound is >= 1 and <= 32 * 1024 * 1024 && uri.Scheme == "https" && uri.UserInfo.Length == 0, "UPDATE_FETCH_BOUND");
        for (int attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd("GCMod-NG03/1.0");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                ContractRules.Require(response.RequestMessage?.RequestUri == null || response.RequestMessage.RequestUri == uri, "UPDATE_REDIRECT_FORBIDDEN");
                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode is >= 500 and <= 599)
                {
                    if (attempt == 2) throw new HttpRequestException("UPDATE_RETRY_BOUND");
                    metrics.Retries++;
                    var delay = response.Headers.RetryAfter?.Delta?.TotalMilliseconds ?? 25 * (attempt + 1);
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(delay, 0, 2000)), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                ContractRules.Require((int)response.StatusCode is < 300 or >= 400, "UPDATE_REDIRECT_FORBIDDEN");
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is { } size) ContractRules.Require(size <= bound, "UPDATE_RESPONSE_SIZE");
                using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                using var memory = new MemoryStream();
                var buffer = new byte[8192];
                while (true)
                {
                    int count = await stream.ReadAsync(buffer.AsMemory(), timeout.Token).ConfigureAwait(false);
                    if (count == 0) break;
                    ContractRules.Require(memory.Length + count <= bound, "UPDATE_RESPONSE_SIZE");
                    memory.Write(buffer, 0, count);
                }
                var bytes = memory.ToArray();
                if (metadata) { metrics.MetadataRequests++; metrics.MetadataBytes += bytes.LongLength; }
                else { metrics.PayloadDownloads++; metrics.PayloadBytes += bytes.LongLength; }
                return bytes;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { if (attempt == 2) throw new HttpRequestException("UPDATE_TIMEOUT_BOUND"); metrics.Retries++; }
            catch (HttpRequestException ex) when (attempt < 2 && (ex.StatusCode == null || ex.StatusCode == HttpStatusCode.TooManyRequests || (int)ex.StatusCode >= 500))
            { metrics.Retries++; }
        }
        throw new HttpRequestException("UPDATE_RETRY_BOUND");
    }
    static bool Reuse(string existing, string destination, PackageFile file, UpdateMetrics metrics)
    {
        try
        {
            if (OperatingSystem.IsWindows() ? CreateHardLink(destination, existing, IntPtr.Zero) : Link(existing, destination) == 0) metrics.OsLinks++;
            else { File.Copy(existing, destination, false); metrics.OsCopies++; metrics.OsCopyBytes += file.Size; }
            metrics.ReusedFiles++;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool CreateHardLink(string destination, string existing, IntPtr securityAttributes);
    [DllImport("libc", EntryPoint = "link", SetLastError = true)] static extern int Link(string existing, string destination);
}
