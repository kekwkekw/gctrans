using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GCMod.Services;

// Binary data is kept outside the text cache. Publish the catalog only after
// every content-addressed PNG is verified, so an interrupted update keeps the
// previous complete image set. No Unity API is called from download workers.
public static class ImageCache
{
    public static async Task PrepareAsync(HttpClient client, string remoteRoot, string localRoot,
        string expectedHash, CancellationToken token)
    {
        if (expectedHash == null) return;
        string catalogPath = Path.Combine(localRoot, "images.json");
        try
        {
            string json = File.Exists(catalogPath) ? await File.ReadAllTextAsync(catalogPath, token).ConfigureAwait(false) : null;
            if (!CatalogHashMatches(json, expectedHash))
                json = System.Text.Encoding.UTF8.GetString(await Download(client, remoteRoot + "/images.json", 128 * 1024, token).ConfigureAwait(false));
            if (ImageCatalog.Hash(json) != expectedHash) throw new FormatException("Image catalog hash mismatch");
            var catalog = ImageCatalog.Parse(json);
            // Full recollection catalogs must fit the existing startup budget.
            // Download at most four distinct payloads concurrently; publication
            // still waits until every hash-verified file is present.
            await Parallel.ForEachAsync(catalog.Entries.GroupBy(entry => entry.File),
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token }, async (targets, downloadToken) =>
            {
                var entry = targets.First();
                string path = Path.Combine(localRoot, entry.File.Replace('/', Path.DirectorySeparatorChar));
                byte[] bytes = File.Exists(path) && new FileInfo(path).Length == entry.Bytes
                    ? await File.ReadAllBytesAsync(path, downloadToken).ConfigureAwait(false) : null;
                if (targets.All(target => target.Matches(bytes))) return;
                bytes = await Download(client, remoteRoot + "/" + entry.File, entry.Bytes, downloadToken).ConfigureAwait(false);
                if (!targets.All(target => target.Matches(bytes))) throw new FormatException("Image content mismatch: " + entry.Name);
                await Save(path, bytes, downloadToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
            await Save(catalogPath, System.Text.Encoding.UTF8.GetBytes(json), token).ConfigureAwait(false);
            Logger.Info("Image data ready: " + catalog.Entries.Count + " verified targets");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is UnauthorizedAccessException ||
                                   ex is FormatException || ex is System.Text.Json.JsonException || ex is OperationCanceledException)
        {
            Logger.Warn("Keeping bundled/cached images: " + ex.Message);
        }
    }

    private static bool CatalogHashMatches(string json, string expectedHash)
    {
        if (json == null) return false;
        try { return ImageCatalog.Hash(json) == expectedHash; }
        catch (Exception ex) when (ex is System.Text.Json.JsonException || ex is InvalidOperationException) { return false; }
    }

    private static async Task<byte[]> Download(HttpClient client, string url, int maxBytes, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maxBytes) throw new IOException("Image download exceeds size limit");
        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var output = new MemoryStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
        {
            if (output.Length + count > maxBytes) throw new IOException("Image download exceeds size limit");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private static async Task Save(string path, byte[] bytes, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
