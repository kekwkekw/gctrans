using System.Collections.Generic;
using DMM.OLG.Unity.Extensions.Novel;
using GCMod.Services;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace GCMod.Patches;

// Use TMP's per-component material instance. TMP owns its lifetime and fallback references.
[HarmonyPatch]
public static class TextStylePatch
{
    private sealed class TextState
    {
        public TextMeshProUGUI Text;
        public bool IsName;
        public readonly ValueOverride<Color> Color = new(DialogueMaterialStyle.SameColor);
        public DialogueMaterialStyle Style;
    }
    private sealed class SpacingState
    {
        public EventText Owner;
        public readonly ValueOverride<float> Spacing = new();
    }
    private static readonly Dictionary<int, TextState> Texts = new();
    private static readonly Dictionary<long, SpacingState> Spacing = new();

    private static Color Rgb(Color value) => new(value.r, value.g, value.b, 1f);

    private static void Apply(TextState state)
    {
        var text = state.Text;
        if (text == null) return;
        if (!DialogueSettings.ReadabilityEnabled) { Restore(state); return; }
        if (state.Style != null && state.Style.Material != text.fontSharedMaterial)
        {
            // The game assigned a different font/material. Release our old property overrides.
            state.Style.Restore();
            state.Style = null;
        }
        if (text.fontSharedMaterial == null) return;
        state.Style ??= new DialogueMaterialStyle(GetInstance(text));
        state.Style.Apply(Config.FaceDilate.Value, Config.OutlineColor, Config.OutlineWidth.Value, Config.OutlineSoftness.Value);
        // Alpha belongs to the native typewriter/fade animation. Preserve it on both paths.
        var color = text.color;
        var styled = state.Color.Apply(Rgb(color), Rgb(state.IsName ? Config.NameTextColor : Config.MessageTextColor));
        styled.a = color.a;
        text.color = styled;
        RefreshMesh(text);
    }

    private static Material GetInstance(TextMeshProUGUI text)
    {
        var source = text.fontSharedMaterial;
        var instance = text.m_fontMaterial;
        if (instance != null && instance != source)
        {
            // Native glyph pooling can reassign the shared preset. Reuse TMP's existing
            // private instance instead of asking fontMaterial to allocate another each time.
            instance.shader = source.shader;
            instance.CopyPropertiesFromMaterial(source);
            text.fontSharedMaterial = instance;
            return instance;
        }
        // First use: TMP creates and registers its own private instance. GCMod never destroys it.
        return text.fontMaterial;
    }

    private static void Restore(TextState state)
    {
        state.Style?.Restore();
        if (state.Text != null)
        {
            var color = state.Text.color;
            var restored = state.Color.Restore(Rgb(color));
            restored.a = color.a;
            state.Text.color = restored;
            RefreshMesh(state.Text);
        }
    }

    private static void RefreshMesh(TextMeshProUGUI text)
    {
        // Rebuild on the next normal canvas pass, including fallback-material property updates.
        // Do not reparse EventText, advance the dialogue, or reset the typewriter/fade.
        text.UpdateMeshPadding();
        text.havePropertiesChanged = true;
        text.SetVerticesDirty();
        text.SetMaterialDirty();
    }

    private static void Track(TextMeshProUGUI text, bool name)
    {
        if (text == null) return;
        int id = text.GetInstanceID();
        if (!Texts.TryGetValue(id, out var state))
            Texts[id] = state = new TextState { Text = text };
        state.IsName = name;
        Apply(state);
    }

    public static void ApplyTracked()
    {
        var expired = new List<int>();
        foreach (var pair in Texts)
        {
            if (pair.Value.Text == null) { Restore(pair.Value); expired.Add(pair.Key); }
            else Apply(pair.Value);
        }
        foreach (var id in expired) Texts.Remove(id);
        foreach (var state in Spacing.Values) ApplySpacing(state);
    }

    private static void ApplySpacing(SpacingState state)
    {
        state.Owner.fontSpacing = DialogueSettings.ReadabilityEnabled
            ? state.Spacing.Apply(state.Owner.fontSpacing, Config.CharacterSpacing.Value)
            : state.Spacing.Restore(state.Owner.fontSpacing);
    }

    public static void PrepareText(EventText owner)
    {
        long id = owner.Pointer.ToInt64();
        if (!Spacing.TryGetValue(id, out var state))
            Spacing[id] = state = new SpacingState { Owner = owner };
        ApplySpacing(state);
    }

    // Return pooled glyphs with original shader properties/color. Their TMP instance stays alive.
    [HarmonyPrefix, HarmonyPatch(typeof(EventText), "UnUseTextObj")]
    public static void ReleaseGlyph(GameObject __0)
    {
        if (__0 == null) return;
        foreach (var text in __0.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            int id = text.GetInstanceID();
            if (Texts.TryGetValue(id, out var state)) { Restore(state); Texts.Remove(id); }
        }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(EventMessage), "SetName")]
    public static void BeforeName(EventMessage __instance)
    {
        var name = __instance.MessageName;
        if (name != null && Texts.TryGetValue(name.GetInstanceID(), out var state))
        { Restore(state); Texts.Remove(name.GetInstanceID()); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(EventMessage), "SetName")]
    public static void AfterName(EventMessage __instance) => Track(__instance.MessageName, true);

    [HarmonyPostfix, HarmonyPatch(typeof(EventMessage), "ChangeParentFrameType")]
    public static void AfterParentFrame(EventMessage __instance) => AfterName(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(EventMessage), "ChangeFrameType")]
    public static void AfterFrame(EventMessage __instance) => AfterName(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(EventText), "SetRuby")]
    public static void AfterLetter(GameObject go, TextMeshProUGUI text)
    {
        Track(text, false);
        // Ruby may be a separate TMP child; limit traversal to this one native glyph object.
        if (go != null)
            foreach (var child in go.GetComponentsInChildren<TextMeshProUGUI>(true))
                Track(child, false);
    }

    public static void Clear()
    {
        foreach (var state in Texts.Values) Restore(state);
        Texts.Clear();
        foreach (var state in Spacing.Values)
            state.Owner.fontSpacing = state.Spacing.Restore(state.Owner.fontSpacing);
        Spacing.Clear();
    }
}
