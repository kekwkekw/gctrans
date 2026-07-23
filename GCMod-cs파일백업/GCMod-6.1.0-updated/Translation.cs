using BepInEx;
using BepInEx.Unity.IL2CPP.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

using Il2CppFontAssetList =
    Il2CppSystem.Collections.Generic.List<TMPro.TMP_FontAsset>;

namespace GCMod
{
    public class Translation
    {
        public static string cdn = "http://localhost:5000";
        public static HttpClient client = new HttpClient();

        public static Dictionary<string, string> names =
            new Dictionary<string, string>();

        public static Dictionary<string, string> words =
            new Dictionary<string, string>();

        public static Dictionary<int, Dictionary<string, string>> novels =
            new Dictionary<int, Dictionary<string, string>>();

        public static AssetBundle fontBundle = null;
        public static TMP_FontAsset fontAsset = null;

        private static bool fontAssetLoading = false;

        public static void Initialize()
        {
            cdn = Config.TranslationCDN.Value;

            // GCMod 자체 번역 데이터는 설정이 켜졌을 때만 불러옵니다.
            LoadTranslation();

            // 폰트 로드는 Translation 설정과 무관하게 실행합니다.
            // 따라서 GCMod 번역은 끄고 XUnity AutoTranslator만 사용해도
            // GCMod가 읽은 TMP 폰트를 글로벌 fallback으로 등록할 수 있습니다.
            Plugin.Instance.StartCoroutine(LoadFontAsset());
        }

        public static async Task<T> GetAsync<T>(string url)
            where T : class
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<T>();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Error: {e.Message}");
            }

            return null;
        }

        public static async Task LoadTranslation()
        {
            if (!Config.Translation.Value)
            {
                return;
            }

            Task<Dictionary<string, string>> nameTask =
                GetAsync<Dictionary<string, string>>(
                    $"{cdn}/names/zh_Hans.json"
                );

            Task<Dictionary<string, string>> wordTask =
                GetAsync<Dictionary<string, string>>(
                    $"{cdn}/words/zh_Hans.json"
                );

            await Task.WhenAll(nameTask, wordTask);

            if (nameTask.Result != null)
            {
                names = nameTask.Result;

                Plugin.Log.LogInfo(
                    $"Character names translation loaded. Total: {names.Count}"
                );
            }
            else
            {
                Plugin.Log.LogWarning(
                    "Character names translation load failed"
                );
            }

            if (wordTask.Result != null)
            {
                words = wordTask.Result;

                Plugin.Log.LogInfo(
                    $"Character words translation loaded. Total: {words.Count}"
                );
            }
            else
            {
                Plugin.Log.LogWarning(
                    "Character words translation load failed"
                );
            }
        }

        public static void LoadFontBundle()
        {
            if (fontBundle != null)
            {
                return;
            }

            string configuredPath = Config.FontBundlePath.Value;

            string bundlePath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(Paths.PluginPath, configuredPath);

            if (!File.Exists(bundlePath))
            {
                Plugin.Log.LogError(
                    $"FontBundle path does not exist: {bundlePath}"
                );

                return;
            }

            try
            {
                fontBundle = AssetBundle.LoadFromFile(bundlePath);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(
                    $"Font bundle load threw an exception: {e}"
                );

                fontBundle = null;
                return;
            }

            if (fontBundle == null)
            {
                Plugin.Log.LogError(
                    $"AssetBundle.LoadFromFile returned null: {bundlePath}"
                );
            }
        }

        public static IEnumerator LoadFontAsset()
        {
            if (fontAsset != null)
            {
                RegisterLoadedFontAsGlobalFallback(fontAsset);
                yield break;
            }

            if (fontAssetLoading)
            {
                yield break;
            }

            fontAssetLoading = true;

            LoadFontBundle();

            if (fontBundle == null)
            {
                fontAssetLoading = false;
                Plugin.Log.LogError("Font bundle load failed");
                yield break;
            }

            AssetBundleRequest request;

            try
            {
                request = fontBundle.LoadAssetAsync(
                    Config.FontAssetName.Value
                );
            }
            catch (Exception e)
            {
                fontAssetLoading = false;

                Plugin.Log.LogError(
                    $"Font asset request failed: {e}"
                );

                yield break;
            }

            yield return request;

            fontAssetLoading = false;

            if (request == null || request.asset == null)
            {
                Plugin.Log.LogError(
                    $"TMP_FontAsset '{Config.FontAssetName.Value}' " +
                    "was not found in the font bundle."
                );

                yield break;
            }

            try
            {
                fontAsset = request.asset.TryCast<TMP_FontAsset>();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(
                    $"TMP_FontAsset cast failed: {e}"
                );

                yield break;
            }

            if (fontAsset == null)
            {
                Plugin.Log.LogError(
                    $"Asset '{Config.FontAssetName.Value}' was loaded, " +
                    "but it could not be cast to TMP_FontAsset."
                );

                yield break;
            }

            // GCMod가 TMP_FontAsset을 성공적으로 얻은 직후,
            // TextMeshPro의 글로벌 fallback 목록에 등록합니다.
            RegisterLoadedFontAsGlobalFallback(fontAsset);

            Plugin.Log.LogInfo(
                $"TMP_FontAsset {fontAsset.name} is loaded"
            );
        }

        public static void RegisterLoadedFontAsGlobalFallback(
            TMP_FontAsset loadedFont
        )
        {
            if (loadedFont == null)
            {
                Plugin.Log.LogWarning(
                    "TMP fallback registration skipped: font is null."
                );

                return;
            }

            try
            {
                Il2CppFontAssetList fallbackFonts =
                    TMP_Settings.fallbackFontAssets;

                if (fallbackFonts == null)
                {
                    fallbackFonts = new Il2CppFontAssetList();
                    TMP_Settings.fallbackFontAssets = fallbackFonts;
                }

                if (ContainsFont(fallbackFonts, loadedFont))
                {
                    Plugin.Log.LogInfo(
                        $"TMP global fallback already contains: " +
                        $"{loadedFont.name}"
                    );

                    return;
                }

                fallbackFonts.Add(loadedFont);

                Plugin.Log.LogInfo(
                    $"TMP global fallback registered: {loadedFont.name}, " +
                    $"fallbackCount={fallbackFonts.Count}"
                );
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(
                    $"TMP global fallback registration failed: {e}"
                );
            }
        }

        private static bool ContainsFont(
            Il2CppFontAssetList fallbackFonts,
            TMP_FontAsset targetFont
        )
        {
            if (fallbackFonts == null || targetFont == null)
            {
                return false;
            }

            int targetInstanceId = targetFont.GetInstanceID();

            for (int i = 0; i < fallbackFonts.Count; i++)
            {
                TMP_FontAsset existingFont = fallbackFonts[i];

                if (existingFont == null)
                {
                    continue;
                }

                if (existingFont == targetFont)
                {
                    return true;
                }

                if (existingFont.GetInstanceID() == targetInstanceId)
                {
                    return true;
                }
            }

            return false;
        }

        public static async Task GetNovelTranslationAsync(int novelId)
        {
            if (novels.ContainsKey(novelId))
            {
                return;
            }

            Dictionary<string, string> translations =
                await GetAsync<Dictionary<string, string>>(
                    $"{cdn}/novels/{novelId}/zh_Hans.json"
                );

            if (translations != null)
            {
                novels[novelId] = translations;

                Plugin.Log.LogInfo(
                    $"Scenario translation loaded. " +
                    $"Total: {translations.Count}"
                );
            }
            else
            {
                Plugin.Log.LogWarning(
                    $"Translations loaded failed: {novelId}"
                );
            }
        }
    }
}
