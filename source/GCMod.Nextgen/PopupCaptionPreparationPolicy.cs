#nullable enable
namespace GCMod.Nextgen;

/// Readiness is sampled by the existing bounded owner retries, never by a new hook.
/// This policy carries managed identity fingerprints only; it is not native evidence.
public static class PopupCaptionPreparationPolicy
{
    static readonly HashSet<string> Skill = new(StringComparer.Ordinal) {
        "skill_damage_type_physical", "skill_popup_title", "skill_level", "skill_owner_style",
        "skill_accuracy_modifier", "skill_critical_modifier", "skill_required_passion", "skill_cooldown",
        "skill_range", "skill_area", "skill_close" };
    static readonly HashSet<string> Exchange = new(StringComparer.Ordinal) {
        "exchange_material_select_title", "exchange_output_owned_count_label", "exchange_acquired_count_label",
        "exchange_conversion_result_title", "exchange_close", "exchange_convert_button",
        "exchange_conversion_title_exact_crlf", "exchange_material_deselect", "exchange_conversion_help" };
    public static bool Supports(UiBinding b) => b.Enabled && b.Producer == null &&
        b.SourceIdentity.Kind == "current-model" && b.Selector.Lifetime == "owner" &&
        b.Selector.Segments.Length > 0 &&
        (b.Selector.ScopeKey == "gc.activeskilldetailpopup" && b.Selector.Type == "caption" &&
            b.AdapterId is "ui.chrome.v1" or "ui.caption.v1" && Skill.Contains(b.Selector.SlotKey) ||
         b.Selector.ScopeKey == "gc.exchangelayerpiecepopup" && b.Selector.Type == "exchange" &&
            b.AdapterId is "ui.chrome.v1" or "ui.caption.v1" or "ui.residual-caption.v1" && Exchange.Contains(b.Selector.SlotKey));
    public static bool UsesScopedStatus(UiBinding b) => Supports(b) && b.Selector.ScopeKey == "gc.activeskilldetailpopup" &&
        b.Selector.SlotKey is "skill_accuracy_modifier" or "skill_critical_modifier" or "skill_required_passion" or "skill_cooldown";
}

public sealed class PopupCaptionReadiness
{
    int lastFrame = -1;
    string last = "";
    public bool Observe(bool mainThread, bool currentIdentity, bool active, bool exiting, bool cleanSourceMesh,
        int frame, string fingerprint)
    {
        if (!mainThread || !currentIdentity || !active || exiting || !cleanSourceMesh || frame < 0 || fingerprint.Length == 0)
        { lastFrame = -1; last = ""; return false; }
        bool stable = lastFrame >= 0 && frame > lastFrame && frame - lastFrame <= 120 && last == fingerprint;
        if (lastFrame != frame) { lastFrame = frame; last = fingerprint; }
        return stable;
    }
}
