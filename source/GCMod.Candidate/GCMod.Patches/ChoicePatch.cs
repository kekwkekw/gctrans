using System;
using DMM.OLG.Unity.Extensions.Novel;
using HarmonyLib;
using UnityEngine.Events;

namespace GCMod.Patches;

// EventSelect renders its label one glyph at a time, bypassing whole-string TMP UI lookup.
// Translate only its display string. The game's separate UnityAction/branch closure is untouched.
[HarmonyPatch(typeof(EventSelect), "AddSelectMessage", new Type[] { typeof(string), typeof(UnityAction) })]
public static class ChoicePatch
{
    [HarmonyPrefix]
    public static void TranslateLabel(ref string __0)
    {
        if (Config.Translation.Value && UiPatch.TryTranslate(__0, out var translated))
            __0 = translated;
    }
}
