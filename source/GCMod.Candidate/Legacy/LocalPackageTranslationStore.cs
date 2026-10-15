using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using GCMod.Nextgen;
using GCMod.Services;
namespace GCMod.Candidate;

/// <summary>Local immutable inventory supplies all stored Novel IDs, irrespective of legacy registration.</summary>
public sealed class LocalPackageTranslationStore : ITranslationCache
{
    readonly TranslationPackageSnapshot snapshot;
    readonly ConcurrentDictionary<(string, Type), object> memory = new();
    readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.Ordinal);
    Manifest manifest;
    public string Origin => snapshot.Origin;
    public string AuthorityIdentity => snapshot.ComponentSignatures["master"];
    public LocalPackageTranslationStore(TranslationPackageSnapshot snapshot, string language = "ko")
    {
        if (!CandidateLocalePolicy.IsSupported(language)) throw new ArgumentException("LOCAL_KOREAN_LOCALE_OR_LEGACY_ALIAS_REQUIRED");
        this.snapshot = snapshot;
    }
    static string Relative(string type, string id) => type switch
    {
        TranslationPaths.Manifest => "manifest.json",
        TranslationPaths.Names => "names.json",
        TranslationPaths.Master => "master.json",
        TranslationPaths.Novels when id != null && id.Length is >= 1 and <= 16 && id.All(c => c is >= '0' and <= '9') => "novels/" + id + ".json",
        _ => throw new ArgumentException("UNKNOWN_LOCAL_TRANSLATION_RESOURCE")
    };
    public async Task<Manifest> FetchManifestAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync<Manifest>(TranslationPaths.Manifest, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (loaded != null) manifest = loaded;
        return loaded;
    }
    public Task<T> LoadLocalAsync<T>(string type, string id = null, CancellationToken cancellationToken = default) where T : class
        => LoadAsync<T>(type, id, cancellationToken);
    public async Task<T> LoadAsync<T>(string type, string id = null, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var relative = Relative(type, id);
        var gate = locks.GetOrAdd(relative, _ => new SemaphoreSlim(1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (memory.TryGetValue((relative, typeof(T)), out var ready)) return (T)ready;
            // Missing inventory entry is an ordinary resource miss; manifest registration does not deny reads.
            var bytes = await snapshot.Payloads.ReadAsync(relative, cancellationToken).ConfigureAwait(false);
            if (bytes == null) return null;
            var data = JsonSerializer.Deserialize<T>(bytes);
            if (data == null) return null;
            string expected = type switch
            {
                TranslationPaths.Master => manifest?.Master,
                TranslationPaths.Names => manifest?.Names,
                TranslationPaths.Novels when manifest?.Novels?.TryGetValue(id, out var hash) == true => hash,
                _ => null
            };
            if (expected != null && TranslationHash.Compute(Encoding.UTF8.GetString(bytes)) != expected)
            { Logger.Warn("Local legacy semantic hash mismatch: " + relative); return null; }
            cancellationToken.ThrowIfCancellationRequested();
            memory[(relative, typeof(T))] = data;
            return data;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is JsonException or PackageValidationException or IOException or UnauthorizedAccessException)
        { Logger.Warn("Local translation resource unavailable: " + relative); return null; }
        finally { gate.Release(); }
    }
}
