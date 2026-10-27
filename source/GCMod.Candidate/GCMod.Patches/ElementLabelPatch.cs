using Gc;
using HarmonyLib;

namespace GCMod.Patches;

// Element display names come from the enum helper, not Master data.
// Keep the general UI single-glyph exclusion for Novel typewriter text.
[HarmonyPatch(typeof(ElementTypeExtensions), nameof(ElementTypeExtensions.ToName))]
public static class ElementLabelPatch
{
    [HarmonyPostfix]
    public static void TranslateName(ref string __result)
    {
        if (!Config.Translation.Value) return;
        __result = __result switch
        {
            "物理" => "물리",
            "火" => "불",
            "水" => "물",
            "風" => "바람",
            "雷" => "번개",
            _ => __result
        };
    }
}
