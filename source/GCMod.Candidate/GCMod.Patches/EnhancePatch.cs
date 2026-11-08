using Gc;
using GCMod.Services;
using HarmonyLib;

namespace GCMod.Patches;

[HarmonyPatch]
public static class EnhancePatch
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(GcOptionData), "SetPowerSaving")]
	public static void ChangeFrameRate() => FrameRateSettings.NativeSettingsApplied();

}
