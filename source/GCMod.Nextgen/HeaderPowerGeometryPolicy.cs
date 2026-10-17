#nullable enable
namespace GCMod.Nextgen;

/// The accepted power heading is a child of the header's live numeric TMP.
/// Only this literal role can use its owned local layout while the header slides.
public static class HeaderPowerGeometryPolicy
{
    static readonly string[] CaptionPath = { "Frame_UI", "Group_Left", "Frame_Toplayer", "CombatPower", "Text_PlayerCombatPower", "Text_CombatPowerHeader" };
    public static bool Supports(UiBinding binding) => binding.Enabled && binding.AdapterId == "ui4.static-caption.v1"
        && binding.SourceIdentity.Kind == "static-asset" && binding.Selector.ScopeKey == "gc.headertopcontroller"
        && binding.Selector.Lifetime == "owner" && binding.Selector.Type == "static"
        && binding.Selector.Segments.SequenceEqual(CaptionPath, StringComparer.Ordinal)
        && binding.Producer is { LocalizerType: 0, TextConfigKey: "Unit_deck_ui_13", OptionArea: null };
}
