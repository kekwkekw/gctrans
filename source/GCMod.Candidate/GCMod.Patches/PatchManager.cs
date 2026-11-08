using System.Collections.Generic;
using HarmonyLib;

namespace GCMod.Patches;

public static class PatchManager
{
	private static readonly List<Harmony> Installed = new List<Harmony>();

	public static void Initialize()
	{
		if (Installed.Count > 0)
		{
			return;
		}
		try
		{
			Installed.Add(Harmony.CreateAndPatchAll(typeof(EnhancePatch), (string)null));
			Installed.Add(Harmony.CreateAndPatchAll(typeof(TranslationPatch), (string)null));
			Installed.Add(Harmony.CreateAndPatchAll(typeof(VisualPatch), (string)null));
			Installed.Add(Harmony.CreateAndPatchAll(typeof(CgMessageLayoutPatch), (string)null));
			Installed.Add(Harmony.CreateAndPatchAll(typeof(TextStylePatch), (string)null));
			Installed.Add(Harmony.CreateAndPatchAll(typeof(UiPatch), (string)null));
			Installed.Add(Harmony.CreateAndPatchAll(typeof(ImagePatch), (string)null));
			Installed.Add(Harmony.CreateAndPatchAll(typeof(ChoicePatch), (string)null));
			Installed.Add(Harmony.CreateAndPatchAll(typeof(GlossaryPatch), (string)null));
			Installed.Add(Harmony.CreateAndPatchAll(typeof(ElementLabelPatch), (string)null));
			MasterDataPatch.Install();
		}
		catch
		{
			Uninstall();
			throw;
		}
		var settings = new Harmony("GCMod.SettingsUI");
		try
		{
			settings.PatchAll(typeof(SettingsUiPatch));
			Installed.Add(settings);
		}
		catch (System.Exception ex)
		{
			settings.UnpatchSelf();
			Plugin.Log.LogError("[SettingsUI] Optional UI hooks disabled: " + ex);
		}
	}

	public static bool Uninstall()
	{
		if (!MasterDataPatch.TryUninstall())
		{
			return false;
		}
		foreach (Harmony item in Installed)
		{
			item.UnpatchSelf();
		}
		Installed.Clear();
		SettingsUiPatch.Cleanup();
		GCMod.Services.FrameRateSettings.RestoreNative();
		TextStylePatch.Clear();
		VisualPatch.ClearBackgrounds();
		CgMessageLayoutPatch.Clear();
		return true;
	}

	public static bool TryGetCurrentNovel(out Dictionary<string, string> translation)
	{
		translation = null;
		if (Config.Translation.Value)
		{
			return Plugin.Trans.TryGetNovelTranslation(Plugin.Trans.CurrentNovelId, out translation);
		}
		return false;
	}
}
