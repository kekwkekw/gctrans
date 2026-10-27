using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GCMod.Patches;

[HarmonyPatch]
public static class UiPatch
{
	private static Dictionary<string, string> _translations = new Dictionary<string, string>(StringComparer.Ordinal);

	[ThreadStatic]
	private static bool _isTranslating;

	public static void Initialize(string transDir, string language)
	{
		try
		{
			Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.Ordinal);

			string uiPath = Path.Combine(transDir, language, "ui.json");

			if (File.Exists(uiPath))
			{
				string json = File.ReadAllText(uiPath);
				Dictionary<string, string> loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
				if (loaded != null)
				{
					foreach (KeyValuePair<string, string> kvp in loaded)
					{
						if (!string.IsNullOrEmpty(kvp.Key) && !string.IsNullOrEmpty(kvp.Value) && kvp.Key != kvp.Value)
						{
							dict[kvp.Key] = kvp.Value;
						}
					}
				}
				Plugin.Log.LogInfo($"[UiPatch] Loaded {dict.Count} UI translations from {uiPath}");
			}
			else
			{
				Plugin.Log.LogWarning($"[UiPatch] ui.json not found at {uiPath}");
			}

			_translations = dict;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError($"[UiPatch] Failed to load UI translations: {ex}");
		}
	}

	public static bool TryTranslate(string original, out string translated)
	{
		bool found = UiTextLookup.TryTranslate(_translations, original, out translated);
		return found;
	}

	// Exclude editable controls, including inactive/prefab input text. No selector or ownership layer.
	private static bool IsInputText(Component text)
	{
		return text == null || text.GetComponentInParent<TMP_InputField>(true) != null
			|| text.GetComponentInParent<InputField>(true) != null;
	}

	[HarmonyPatch(typeof(TMP_Text), "text", MethodType.Setter)]
	[HarmonyPrefix]
	public static void SetTextPrefix(TMP_Text __instance, ref string value)
	{
		if (_isTranslating || value == null || value.Length <= 1 || !Config.Translation.Value || IsInputText(__instance))
		{
			return;
		}

		if (TryTranslate(value, out string translated))
		{
			value = translated;
		}
	}

	[HarmonyPatch(typeof(TextMeshProUGUI), "OnEnable")]
	[HarmonyPostfix]
	public static void OnEnablePostfix(TextMeshProUGUI __instance)
	{
		if (_isTranslating || (UnityEngine.Object)(object)__instance == (UnityEngine.Object)null || !Config.Translation.Value || IsInputText(__instance))
		{
			return;
		}

		try
		{
			_isTranslating = true;
			string current = ((TMP_Text)__instance).text;
			if (!string.IsNullOrEmpty(current) && current.Length > 1 && TryTranslate(current, out string translated))
			{
				((TMP_Text)__instance).text = translated;
			}
		}
		catch
		{
		}
		finally
		{
			_isTranslating = false;
		}
	}

	// Dynamic labels can bypass text.set through TMP.SetText after OnEnable.
	// Apply the same guarded lookup immediately before TMP generates the display.
	[HarmonyPatch(typeof(TextMeshProUGUI), "OnPreRenderCanvas")]
	[HarmonyPrefix]
	public static void BeforeRenderPrefix(TextMeshProUGUI __instance) => OnEnablePostfix(__instance);

	[HarmonyPatch(typeof(Text), "text", MethodType.Setter)]
	[HarmonyPrefix]
	public static void SetLegacyTextPrefix(Text __instance, ref string value)
	{
		if (_isTranslating || value == null || value.Length <= 1 || !Config.Translation.Value || IsInputText(__instance))
		{
			return;
		}

		if (TryTranslate(value, out string translated))
		{
			value = translated;
		}
	}
}
