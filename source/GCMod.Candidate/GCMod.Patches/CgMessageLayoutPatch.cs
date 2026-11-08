using System.Collections.Generic;
using DMM.OLG.Unity.Extensions.Novel;
using GCMod.Services;
using HarmonyLib;
using UnityEngine;

namespace GCMod.Patches;

// The native CG frame's third line extends below the 16:9 viewport. Move its
// dialogue group together so the name, controls, background and ruby keep their
// relative positions. This is a layout fix, independent of F5 styling/opacity.
[HarmonyPatch]
public static class CgMessageLayoutPatch
{
    // Native UI reference units: 48 = 32 screen pixels at 1280x720.
    // Measured third-line bottom: -10.56px before, approximately 21px after.
    private const float BottomInset = 48f;

    private sealed class LayoutState
    {
        public RectTransform Group;
        public readonly ValueOverride<float> PositionY = new();
        public RectState Background;
        public readonly List<RectState> BackgroundChildren = new();
    }

    private sealed class RectState
    {
        public RectTransform Rect;
        public readonly ValueOverride<float> PositionY = new();
        public readonly ValueOverride<float> SizeY = new();

        public void Adjust(float positionOffset, float sizeOffset)
        {
            if (Rect == null) return;
            var size = Rect.sizeDelta;
            size.y = SizeY.Apply(size.y, SizeY.Source(size.y) + sizeOffset);
            Rect.sizeDelta = size;
            var position = Rect.anchoredPosition;
            position.y = PositionY.Apply(position.y, PositionY.Source(position.y) + positionOffset);
            Rect.anchoredPosition = position;
        }

        public void Restore()
        {
            if (Rect == null) return;
            var size = Rect.sizeDelta;
            size.y = SizeY.Restore(size.y);
            Rect.sizeDelta = size;
            var position = Rect.anchoredPosition;
            position.y = PositionY.Restore(position.y);
            Rect.anchoredPosition = position;
        }
    }

    private static readonly Dictionary<int, LayoutState> Layouts = new();

    [HarmonyPostfix, HarmonyPatch(typeof(EventMessage), "Init")]
    public static void AfterInit(EventMessage __instance) => Apply(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(EventMessage), "OnEnable")]
    public static void AfterEnable(EventMessage __instance) => Apply(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(EventMessage), "ChangeParentFrameType")]
    public static void AfterParentFrame(EventMessage __instance) => Apply(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(EventMessage), "ChangeFrameType")]
    public static void AfterFrame(EventMessage __instance) => Apply(__instance);

    private static void Apply(EventMessage __instance)
    {
        if (__instance == null || __instance.parentFrames == null) return;
        var expired = new List<int>();
        foreach (var entry in Layouts)
            if (entry.Value.Group == null) expired.Add(entry.Key);
        foreach (var id in expired) Layouts.Remove(id);

        foreach (var frame in __instance.parentFrames)
        {
            if (frame == null || frame.name != "CgModeFrame") continue;
            var group = frame.transform.Find("MainMessage")?.TryCast<RectTransform>();
            if (group == null) continue;
            int id = group.GetInstanceID();
            if (!Layouts.TryGetValue(id, out var state))
            {
                Layouts[id] = state = new LayoutState { Group = group };
                // The CG background includes the speaker label as a child. Extend
                // its bottom edge without moving that label or changing its width.
                var background = frame.transform.Find("MainMessage/Image_Message/MessageWindow/MessageWindow")?.TryCast<RectTransform>();
                if (background != null)
                {
                    state.Background = new RectState { Rect = background };
                    for (int i = 0; i < background.childCount; i++)
                    {
                        var child = background.GetChild(i).TryCast<RectTransform>();
                        if (child != null) state.BackgroundChildren.Add(new RectState { Rect = child });
                    }
                }
                Plugin.Log.LogInfo($"[CGLayout] Bound dialogue group {id}; vertical inset={BottomInset}");
            }
            var current = group.anchoredPosition;
            current.y = state.PositionY.Apply(current.y, state.PositionY.Source(current.y) + BottomInset);
            group.anchoredPosition = current;
            if (state.Background?.Rect != null)
            {
                var background = state.Background.Rect;
                // Keep the top fixed; the group lift and this extension cancel at
                // the bottom, leaving the backdrop flush with its native edge.
                state.Background.Adjust(-BottomInset * (1f - background.pivot.y), BottomInset);
                foreach (var child in state.BackgroundChildren)
                {
                    if (child.Rect == null) continue;
                    var rect = child.Rect;
                    float anchor = Mathf.Lerp(rect.anchorMin.y, rect.anchorMax.y, rect.pivot.y);
                    child.Adjust(BottomInset * (1f - anchor),
                        -BottomInset * (rect.anchorMax.y - rect.anchorMin.y));
                }
            }
        }
    }

    public static void Clear()
    {
        foreach (var state in Layouts.Values)
        {
            foreach (var child in state.BackgroundChildren) child.Restore();
            state.Background?.Restore();
            if (state.Group != null)
            {
                var position = state.Group.anchoredPosition;
                position.y = state.PositionY.Restore(position.y);
                state.Group.anchoredPosition = position;
            }
        }
        Layouts.Clear();
    }
}
