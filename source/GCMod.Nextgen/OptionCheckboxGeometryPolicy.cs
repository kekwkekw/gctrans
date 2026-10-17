#nullable enable
using System.Collections.Immutable;
namespace GCMod.Nextgen;

/// Only the accepted immediate-display caption is rendered in its native checkbox composite.
/// The typed area/index contract and current direct-caption or explicit graphic role are required at runtime.
public enum OptionCheckboxCaptionRole { Unavailable, GraphicMember, DeclaredDirectCaption }
public sealed record OptionCheckboxCaptionObservation(long TargetTransform, long ParentTransform, long DeclaredControlTransform,
    long IndexedTargetTransform, string TargetName, int SameNamedChildren, int TargetTmpCount);
public static class OptionCheckboxGeometryPolicy
{
    static readonly string[] CaptionPath = { "Inner_Frame", "Group_DisplaySpeed", "CheckBox", "Text_Color" };
    static readonly int[] CaptionIndices = { 1, 2, 1, 1 };
    public static bool Supports(UiBinding binding) => binding.Enabled && binding.AdapterId == "ui4.static-caption.v1"
        && binding.SourceIdentity.Kind == "static-asset" && binding.Selector.ScopeKey == "gc.option.optionpopup"
        && binding.Selector.Lifetime == "owner" && binding.Selector.Type == "static"
        && binding.Producer is { LocalizerType: 0, TextConfigKey: "OptionPopup_3", OptionArea: not null } producer
        && producer.OptionArea.Field == "soundAndTextOptionArea"
        && producer.OptionArea.Segments.SequenceEqual(CaptionPath, StringComparer.Ordinal)
        && producer.OptionArea.Indices.SequenceEqual(CaptionIndices);

    public static OptionCheckboxCaptionRole SelectRole(UiBinding binding, IReadOnlyList<long> graphics, long targetComponent,
        OptionCheckboxCaptionObservation current)
    {
        if (!Supports(binding) || current.TargetTransform == 0 || current.DeclaredControlTransform == 0
            || current.ParentTransform != current.DeclaredControlTransform || current.IndexedTargetTransform != current.TargetTransform
            || current.TargetName != "Text_Color" || current.SameNamedChildren != 1 || current.TargetTmpCount != 1
            || graphics.Count > 32) return OptionCheckboxCaptionRole.Unavailable;
        if (graphics.Count == 0) return targetComponent != 0 ? OptionCheckboxCaptionRole.DeclaredDirectCaption : OptionCheckboxCaptionRole.Unavailable;
        return UniqueGraphicMember(graphics, targetComponent) >= 0 ? OptionCheckboxCaptionRole.GraphicMember : OptionCheckboxCaptionRole.Unavailable;
    }

    public static int UniqueGraphicMember(IReadOnlyList<long> graphics, long target)
    {
        if (target == 0 || graphics.Count is <= 0 or > 32) return -1;
        int found = -1;
        for (int i = 0; i < graphics.Count; i++) if (graphics[i] == target)
        { if (found >= 0) return -1; found = i; }
        return found;
    }
    public static bool PlainSdfMaterial(string shader, IEnumerable<string> keywords)
        => shader is "TextMeshPro/Mobile/Distance Field" or "TextMeshPro/Distance Field"
            && keywords.Take(33).ToArray() is var words && words.Length <= 32
            && words.All(word => word is "UNITY_UI_CLIP_RECT" or "UNITY_UI_ALPHACLIP");
}
