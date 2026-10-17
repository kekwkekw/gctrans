#nullable enable
namespace GCMod.Nextgen;

/// Only the accepted Filter reset, deselect and distinct SelectAll captions follow native preferred-size drivers.
/// The adapter separately proves the exact typed button field, owned target and current driver.
public enum FilterPreferredCaptionRole { Unavailable, Reset, Deselect, SelectAll }
public static class FilterResetGeometryPolicy
{
    static readonly string[] ResetPath = { "Frame_Popup", "PopupWindowM", "PopupBase", "ContentsArea", "Frame_Content", "Group_Bt", "BtDefaultAll", "image_Textbtn01_s", "Group_Text", "Text_Textbtn01" };
    static readonly string[] DeselectPath = { "Frame_Popup", "PopupWindowM", "PopupBase", "ContentsArea", "Frame_Content", "Group_Bt", "BtDeselectAll", "image_Textbtn01_s", "Group_Text", "Text_Textbtn01" };
    static readonly string[] SelectAllPath = { "Frame_Popup", "PopupWindowM", "PopupBase", "ContentsArea", "Frame_Content", "Group_Bt", "BtSelectAll", "image_Textbtn01_s", "Group_Text", "Text_Textbtn01" };
    public static FilterPreferredCaptionRole Role(UiBinding binding)
    {
        if (!binding.Enabled || binding.AdapterId != "ui4.static-caption.v1" || binding.SourceIdentity.Kind != "static-asset"
            || binding.Selector.ScopeKey != "gc.filterpopup" || binding.Selector.Lifetime != "owner" || binding.Selector.Type != "static")
            return FilterPreferredCaptionRole.Unavailable;
        if (binding.Selector.SlotKey == "ui4-ui4-s089-62-06007a91" && binding.Selector.Segments.SequenceEqual(ResetPath, StringComparer.Ordinal))
            return FilterPreferredCaptionRole.Reset;
        if (binding.Selector.SlotKey == "ui4-ui4-s064-62-06007a91" && binding.Selector.Segments.SequenceEqual(DeselectPath, StringComparer.Ordinal))
            return FilterPreferredCaptionRole.Deselect;
        if (binding.SourceIdentity.ExactJp == "全て選択" && binding.Selector.SlotKey == "ui4-ui4-s085-82-06007a91"
            && binding.Selector.Segments.SequenceEqual(SelectAllPath, StringComparer.Ordinal) && binding.Producer == null)
            return FilterPreferredCaptionRole.SelectAll;
        return FilterPreferredCaptionRole.Unavailable;
    }
    public static bool Supports(UiBinding binding) => Role(binding) != FilterPreferredCaptionRole.Unavailable;
}
