using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DMM.OLG.Unity.Engine;
using GCMod.Patches;
using GCMod.Services;
using TMPro;
using UnityEngine;
using Utility.Assets;
using Utility.Notifications;

namespace GCMod;

[BepInPlugin("GCMod", "GCMod", "7.3.1")]
public class Plugin : BasePlugin
{
	private const int HttpTimeoutSeconds = 30;

	private const int PooledConnectionLifetimeMinutes = 5;

	private const int PooledConnectionIdleTimeoutMinutes = 2;

	private static HttpClient _httpClient;
	private static bool _loadFailureShown;

	public static ConfigFile ConfigFile;

	public static ManualLogSource Log;

	public static MonoBehaviour Instance;

	public static TranslationManager Trans;

	public override void Load()
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		try
		{
			Console.OutputEncoding = Encoding.UTF8;
		}
		catch
		{
		}
		Log = ((BasePlugin)this).Log;
		_loadFailureShown = false;
		try
		{
			Toast.Initialize();
			ConfigFile = ((BasePlugin)this).Config;
			Log.LogInfo("GCMod Korean LocalParity 7.3.1; upstream 0cdcd9e84fffa438dfdac63176685a6c47a07318");
			GCMod.Config.Initialize();
			DialogueSettings.Initialize();
			Log.LogInfo("[SettingsUI] Integrated build16 (settings UI and battle labels)");
			Instance = (MonoBehaviour)(object)((BasePlugin)this).AddComponent<DialogueSettingsUpdater>();
			Initialize();
			PatchManager.Initialize();
			Trans.Initialize();
		}
		catch
		{
			try
			{
				Unload();
			}
			catch (Exception cleanupError)
			{
				Log.LogWarning("Cleanup after GCMod load failure failed: " + cleanupError);
			}
			// Unload clears pending toasts, so queue the failure after cleanup.
			_loadFailureShown = false;
			NotifyLoadFailure();
			throw;
		}
	}

	internal static void NotifyLoadFailure()
	{
		if (_loadFailureShown)
		{
			return;
		}
		try
		{
			_loadFailureShown = Toast.Error("GCMod", "한글 패치 로드 중 오류가 발생했습니다. BepInEx/LogOutput.log를 확인해 주세요.");
		}
		catch (Exception notificationError)
		{
			Log.LogWarning("Could not display the GCMod load failure notification: " + notificationError.Message);
		}
	}

	private static void Initialize()
	{
		_httpClient = new HttpClient(new SocketsHttpHandler
		{
			PooledConnectionLifetime = TimeSpan.FromMinutes(5.0),
			PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2.0)
		})
		{
			Timeout = TimeSpan.FromSeconds(30.0)
		};
		string value = GCMod.Config.FontBundlePath.Value;
		AssetBundleLoader<TMP_FontAsset> font = new AssetBundleLoader<TMP_FontAsset>(Path.IsPathRooted(value) ? value : Path.Combine(Paths.PluginPath, value));
		string transDir = GCMod.Config.CustomTranslationPath.Value;
		if (string.IsNullOrWhiteSpace(transDir) || !Directory.Exists(transDir))
		{
			transDir = Path.Combine(Paths.PluginPath, "GCMod", "translations");
		}
		Log.LogInfo("Using translations path: " + transDir);
		var startupCache = new TranslationCache(GCMod.Config.TranslationCDN.Value, transDir, GCMod.Config.TranslationLanguage.Value, _httpClient);
		if (GCMod.Config.Translation.Value)
		using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
		{
			try { startupCache.PrepareStartupAsync(timeout.Token).GetAwaiter().GetResult(); }
			catch (OperationCanceledException) { Log.LogWarning("Data update timed out; using bundled/cached translations."); }
		}
		// Reuse the startup data on first load. Manual refresh still creates its
		// normal session; UI changes require restarting the game.
		bool firstSession = true;
		Trans = new TranslationManager(() =>
		{
			if (firstSession) { firstSession = false; return startupCache; }
			return new TranslationCache(GCMod.Config.TranslationCDN.Value, transDir, GCMod.Config.TranslationLanguage.Value, _httpClient);
		}, font);
		if (!UiPatch.Initialize(transDir, GCMod.Config.TranslationLanguage.Value) && GCMod.Config.Translation.Value)
		{
			NotifyLoadFailure();
		}
		ImagePatch.Initialize(transDir, GCMod.Config.TranslationLanguage.Value);
		MasterDataPatch.JsonRewriter = delegate(MasterAttribute attribute, string json)
		{
			if (attribute != null)
			{
				return Trans.TranslateMasterData(attribute.Object, json);
			}
			return json;
		};
	}

	public override bool Unload()
	{
		if (!PatchManager.Uninstall())
		{
			Log.LogWarning((object)"Cannot unload while a master data callback is active; retry after loading finishes");
			return false;
		}
		ImagePatch.Clear();
		Trans?.Dispose();
		MasterDataPatch.JsonRewriter = null;
		_httpClient?.Dispose();
		_httpClient = null;
		if ((UnityEngine.Object)(object)Instance != (UnityEngine.Object)null)
		{
			UnityEngine.Object.Destroy((UnityEngine.Object)(object)Instance);
		}
		Instance = null;
		Toast.Clear();
		return true;
	}
}
