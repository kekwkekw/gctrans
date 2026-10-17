#nullable enable
namespace GCMod.Nextgen;

/// The accepted campaign caption has a separate owner/visibility role from the power heading.
public static class CampaignCaptionPolicy
{
    static readonly string[] CaptionPath = { "Frame_UI", "Group_Left", "Frame_Underlayer", "GroupCanpaign", "FrameCanpaign", "Text_Campaign" };
    public static bool Supports(UiBinding binding) => binding.Enabled && binding.AdapterId == "ui4.static-caption.v1"
        && binding.SourceIdentity.Kind == "static-asset" && binding.Selector.ScopeKey == "gc.headertopcontroller"
        && binding.Selector.Lifetime == "owner" && binding.Selector.Type == "static"
        && binding.Selector.SlotKey == "ui4-ui4-s038-1-06002e55"
        && binding.Selector.Segments.SequenceEqual(CaptionPath, StringComparer.Ordinal)
        && (binding.Producer == null || binding.Producer is { LocalizerType: 0, TextConfigKey: "", OptionArea: null });
}
