#nullable enable
namespace GCMod.Nextgen;

/// Only the current, typed shared shop heading uses its immediately owning native
/// container. This never authorizes a parent search or an ink-bound reduction.
public static class ShopHeaderGeometryPolicy
{
    public static bool Supports(UiBinding binding) => binding.Enabled && binding.AdapterId == "ui.caption.v1"
        && binding.SourceIdentity.Kind == "current-model" && binding.SourceIdentity.ExactJp == "ショップ"
        && binding.Selector.ScopeKey == "gc.shop.shoptopcontroller" && binding.Selector.SlotKey == "shop_title"
        && binding.Selector.Type == "caption" && binding.Selector.Lifetime == "owner"
        && binding.Selector.Segments.Length == 0 && binding.Producer == null;

    // A supported stock SDF shader renders in the measured full mesh triangles.
    // No transparent padding is subtracted, unlike the separate Footer policy.
    public static bool SupportedQuadMaterial(string shader, IEnumerable<string> keywords) =>
        shader is "TextMeshPro/Mobile/Distance Field" or "TextMeshPro/Distance Field"
        && keywords.Take(33).ToArray() is var words && words.Length <= 32
        && words.All(word => word is "UNITY_UI_CLIP_RECT" or "UNITY_UI_ALPHACLIP" or "UNDERLAY_ON" or "UNDERLAY_INNER" or "GLOW_ON" or "BEVEL_ON" or "OUTLINE_ON");
}
