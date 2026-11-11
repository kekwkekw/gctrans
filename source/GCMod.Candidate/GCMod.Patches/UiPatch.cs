using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Gc;

namespace GCMod.Patches;

[HarmonyPatch]
public static class UiPatch
{
	private static Dictionary<string, string> _translations = new Dictionary<string, string>(StringComparer.Ordinal);

	[ThreadStatic]
	private static bool _isTranslating;

	public static bool Initialize(string transDir, string language)
	{
		try
		{
			var path = Path.Combine(transDir, language, "ui.json");
			if (!File.Exists(path))
			{
				Plugin.Log.LogWarning($"UI dictionary unavailable: {path}");
				_translations = new(StringComparer.Ordinal);
				return false;
			}

			var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
			_translations = entries?.Where(entry => entry.Key.Length != 0 && !string.IsNullOrEmpty(entry.Value) &&
					!StringComparer.Ordinal.Equals(entry.Key, entry.Value))
				.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
				?? new(StringComparer.Ordinal);
			Plugin.Log.LogInfo($"UI dictionary ready ({_translations.Count} entries): {path}");
			return _translations.Count != 0;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError($"UI dictionary load error: {ex}");
			return false;
		}
	}

	public static bool TryTranslate(string original, out string translated)
	{
		bool found = UiTextLookup.TryTranslate(_translations, original, out translated);
		return found;
	}

	// Choice/frame switching may restore the original name through TMP.SetText.
	// Only the frame's actual name field may consume Names; body glyphs never do.
	private static bool TryTranslateDisplay(TMP_Text text, string original, out string translated)
	{
		translated = null;
		if (Plugin.Trans != null && Plugin.Trans.Names.TryGetValue(original, out var name))
		{
			var frame = text.GetComponentInParent<EventMessageParentFrame>(true);
			if (frame != null && frame.MessageName == text)
			{
				translated = name;
				return true;
			}
		}
		return TryTranslate(original, out translated);
	}

	[HarmonyPatch(typeof(TMP_Text), "text", MethodType.Setter)]
	[HarmonyPrefix]
	public static void SetTextPrefix(TMP_Text __instance, ref string value)
	{
		if (_isTranslating || string.IsNullOrEmpty(value) || !Config.Translation.Value || UiTextScope.IsProtected(__instance))
		{
			return;
		}

		if (TryTranslateDisplay(__instance, value, out string translated))
		{
			value = translated;
		}
	}

	[HarmonyPatch(typeof(TextMeshProUGUI), "OnEnable")]
	[HarmonyPostfix]
	public static void OnEnablePostfix(TextMeshProUGUI __instance)
	{
		if (_isTranslating || (UnityEngine.Object)(object)__instance == (UnityEngine.Object)null || !Config.Translation.Value || UiTextScope.IsProtected(__instance))
		{
			return;
		}

		_isTranslating = true;
		try
		{
			TMP_Text display = __instance;
			var source = display.text;
			if (string.IsNullOrEmpty(source)) return;
			if (!TryTranslateDisplay(display, source, out var replacement)) return;

			display.text = replacement;
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
		if (_isTranslating || string.IsNullOrEmpty(value) || !Config.Translation.Value || UiTextScope.IsProtected(__instance))
		{
			return;
		}

		if (TryTranslate(value, out string translated))
		{
			value = translated;
		}
	}
}
