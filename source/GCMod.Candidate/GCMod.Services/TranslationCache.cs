using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq;

namespace GCMod.Services;

public sealed class TranslationCache
{
	private readonly string _cdn;

	private readonly string _cacheDir;

	private readonly string _language;

	private readonly HttpClient _client;

	private readonly TimeSpan _retryDelay;

	private volatile Manifest _manifest;

	private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();

	private readonly ConcurrentDictionary<(string, Type), object> _memory = new ConcurrentDictionary<(string, Type), object>();

	private readonly ConcurrentDictionary<string, DateTimeOffset> _retryAfter = new ConcurrentDictionary<string, DateTimeOffset>();

	private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	public Manifest Manifest => _manifest;

	private bool Online => !string.IsNullOrWhiteSpace(_cdn) &&
		!_cdn.Equals("none", StringComparison.OrdinalIgnoreCase) &&
		!_cdn.Equals("offline", StringComparison.OrdinalIgnoreCase) &&
		!_cdn.Contains("anosu/girlscreation-translation", StringComparison.OrdinalIgnoreCase);

	// Run before hooks are installed. UI and Master are then loaded from these
	// same cached values; Novel payloads remain lazy and are checked on entry.
	public async Task PrepareStartupAsync(CancellationToken token)
	{
		await FetchManifestAsync(token).ConfigureAwait(false);
		await Task.WhenAll(LoadAsync("names", cancellationToken: token),
			LoadAsync("ui", cancellationToken: token),
			LoadAsync<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>("master", cancellationToken: token))
			.ConfigureAwait(false);
	}

	public TranslationCache(string cdn, string cacheDir, string language, HttpClient client, TimeSpan? retryDelay = null)
	{
		_cdn = cdn.TrimEnd('/');
		_cacheDir = cacheDir;
		_language = language;
		_client = client;
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(30.0);
	}

	public async Task<Manifest> FetchManifestAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		Manifest manifest = await LoadAsync<Manifest>("manifest", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (manifest != null)
		{
			_manifest = manifest;
		}
		return manifest;
	}

	public Task<Dictionary<string, string>> LoadAsync(string type, string id = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		return LoadAsync<Dictionary<string, string>>(type, id, cancellationToken);
	}

	public async Task<T> LoadLocalAsync<T>(string type, string id = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
	{
		cancellationToken.ThrowIfCancellationRequested();
		string path = TranslationPaths.BuildCachePath(_cacheDir, type, _language, id);
		T result = Deserialize<T>(await ReadLocalAsync(path, cancellationToken).ConfigureAwait(continueOnCapturedContext: false), path);
		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}

	public async Task<T> LoadAsync<T>(string type, string id = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
	{
		cancellationToken.ThrowIfCancellationRequested();
		string key = ((id == null) ? type : (type + "/" + id));
		(string key, Type) memoryKey = (key: key, typeof(T));
		SemaphoreSlim gate = _locks.GetOrAdd(key, (string _) => new SemaphoreSlim(1, 1));
		await gate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (_memory.TryGetValue(memoryKey, out var value))
			{
				return (T)value;
			}
			string path = TranslationPaths.BuildCachePath(_cacheDir, type, _language, id);
			string json = await ReadLocalAsync(path, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			T local = Deserialize<T>(json, path);
			string expectedHash = GetManifestHash(type, id);
			if (local != null && (!Online || (type != "manifest" &&
				(expectedHash == null || MatchesHash(json, expectedHash)))))
			{
				Logger.Info("Loaded local translation: " + _language + "/" + key);
				_memory[memoryKey] = local;
				return local;
			}

			// Never download an unregistered payload. An old/offline manifest
			// without a UI hash keeps the bundled UI instead.
			if (!Online || (type != "manifest" && expectedHash == null))
			{
				return local;
			}

			if (_retryAfter.TryGetValue(key, out var value2) && DateTimeOffset.UtcNow < value2)
			{
				return local;
			}
			string url = TranslationPaths.BuildRemoteUrl(_cdn, type, _language, id);
			string json2 = await DownloadAsync(url, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			T remote = Deserialize<T>(json2, url);
			bool valid = remote != null && (type == "manifest" ? ValidManifest(json2) :
				expectedHash != null && MatchesHash(json2, expectedHash));
			if (remote != null && valid)
			{
				await SaveAsync(path, json2, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				cancellationToken.ThrowIfCancellationRequested();
				_memory[memoryKey] = remote;
				Logger.Info("Updated translation: " + _language + "/" + key +
					(type == "manifest" ? "" : " hash=" + expectedHash));
				return remote;
			}
			cancellationToken.ThrowIfCancellationRequested();
			_retryAfter[key] = DateTimeOffset.UtcNow + _retryDelay;
			Logger.Warn("Keeping bundled/cached translation: " + _language + "/" + key);
			if (local != null) _memory[memoryKey] = local;
			return local;
		}
		finally
		{
			gate.Release();
		}
	}

	private string GetManifestHash(string type, string id)
	{
		Manifest manifest = _manifest;
		switch (type)
		{
		case "names":
			return manifest?.Names;
		case "master":
			return manifest?.Master;
		case "ui":
			return manifest?.Ui;
		case "novels":
		{
			if (id != null && manifest?.Novels != null && manifest.Novels.TryGetValue(id, out var value))
			{
				return value;
			}
			break;
		}
		}
		return null;
	}

	private static bool ValidManifest(string json)
	{
		try
		{
			if (json == null) return false;
			var manifest = JsonSerializer.Deserialize<Manifest>(json);
			bool Hash(string h) => h != null && Regex.IsMatch(h, "^[0-9a-f]{32}$");
			return manifest != null && Hash(manifest.Hash) && Hash(manifest.Names) &&
				Hash(manifest.Master) && (manifest.Ui == null || Hash(manifest.Ui)) &&
				manifest.Novels != null && manifest.Novels.All(p =>
					Regex.IsMatch(p.Key, "^[0-9]{1,16}$") && Hash(p.Value)) &&
				TranslationHash.Compute(JsonSerializer.Serialize(
					JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
						.Where(p => p.Key != "hash").ToDictionary(p => p.Key, p => p.Value))) == manifest.Hash;
		}
		catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
		{
			return false;
		}
	}

	private async Task<string> DownloadAsync(string url, CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			Logger.Info("Fetching translation: " + url);
			using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			requestTimeout.CancelAfter(TimeSpan.FromSeconds(5));
			using var request = new HttpRequestMessage(HttpMethod.Get, url);
			request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
			using HttpResponseMessage response = await _client.SendAsync(request, requestTimeout.Token).ConfigureAwait(continueOnCapturedContext: false);
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex2) when (((ex2 is HttpRequestException || ex2 is OperationCanceledException || ex2 is IOException) ? 1 : 0) != 0)
		{
			Logger.Warn("Translation download failed: " + url + ": " + ex2.Message);
			return null;
		}
	}

	private static async Task<string> ReadLocalAsync(string path, CancellationToken cancellationToken)
	{
		if (!File.Exists(path))
		{
			return null;
		}
		try
		{
			return await File.ReadAllTextAsync(path, Utf8, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			Logger.Warn("Cannot read translation cache: " + path + ": " + ex.Message);
			return null;
		}
	}

	private static T Deserialize<T>(string json, string source) where T : class
	{
		if (json == null)
		{
			return null;
		}
		try
		{
			return JsonSerializer.Deserialize<T>(json);
		}
		catch (JsonException ex)
		{
			Logger.Warn("Invalid translation JSON: " + source + ": " + ex.Message);
			return null;
		}
	}

	private static bool MatchesHash(string json, string expectedHash)
	{
		try
		{
			return TranslationHash.Compute(json) == expectedHash;
		}
		catch (Exception ex) when (((ex is JsonException || ex is InvalidOperationException) ? 1 : 0) != 0)
		{
			Logger.Warn("Invalid translation hash input: " + ex.Message);
			return false;
		}
	}

	private static async Task SaveAsync(string path, string json, CancellationToken cancellationToken)
	{
		string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			await File.WriteAllTextAsync(temporary, json, Utf8, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			cancellationToken.ThrowIfCancellationRequested();
			File.Move(temporary, path, overwrite: true);
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			Logger.Warn("Cannot save translation cache: " + path + ": " + ex.Message);
		}
		finally
		{
			try
			{
				File.Delete(temporary);
			}
			catch (Exception ex2) when (((ex2 is IOException || ex2 is UnauthorizedAccessException) ? 1 : 0) != 0)
			{
				Logger.Warn("Cannot remove temporary cache: " + temporary + ": " + ex2.Message);
			}
		}
	}
}
