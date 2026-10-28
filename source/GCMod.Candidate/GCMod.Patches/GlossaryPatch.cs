using System.Linq;
using Gc;
using Gc.Database;
using HarmonyLib;

namespace GCMod.Patches;

[HarmonyPatch]
public static class GlossaryPatch
{
    private static bool Enabled => Config.Translation.Value && Config.TranslationLanguage.Value == "ko";

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DatabaseTopController), "CreateKeywordCellData")]
    public static void SortKeywords(ref Il2CppSystem.Collections.Generic.List<NovelKeyWordCellData> __result)
    {
        if (!Enabled || __result == null) return;
        var rows = new System.Collections.Generic.List<NovelKeyWordCellData>();
        for (int i = 0; i < __result.Count; i++) rows.Add(__result[i]);
        var sorted = new Il2CppSystem.Collections.Generic.List<NovelKeyWordCellData>();
        foreach (var row in GlossaryIndex.Sort(rows, r => r.KeyWord)) sorted.Add(row);
        __result = sorted;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DatabaseTopController), "CreateIndexCellData")]
    public static bool CreateIndices(Il2CppSystem.Collections.Generic.IReadOnlyList<NovelKeyWordCellData> __0,
        ref Il2CppSystem.Collections.Generic.List<NovelIndexCellData> __result)
    {
        if (!Enabled || __0 == null) return true;
        var titles = new System.Collections.Generic.List<string>();
        int count = __0.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<NovelKeyWordCellData>>().Count;
        for (int i = 0; i < count; i++) titles.Add(__0[i].KeyWord);
        var indices = new Il2CppSystem.Collections.Generic.List<NovelIndexCellData>();
        foreach (var section in GlossaryIndex.Sections(titles))
            indices.Add(new NovelIndexCellData(section.Label, section.Position));
        __result = indices;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelKeyWordDetailParts), "Setup")]
    public static void HideJapaneseRuby(NovelKeyWordDetailParts __instance)
    {
        if (Enabled && __instance.titleText != null && __instance.titleRubyText != null
            && GlossaryIndex.HasHangul(__instance.titleText.text))
            __instance.titleRubyText.text = "";
    }
}
