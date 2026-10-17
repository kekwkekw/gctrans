#nullable enable
using System.Text.Json;

namespace GCMod.Nextgen;

public sealed record Ui6ConsumerRoute(string Identity, string Scope, string Slot, string CallbackType, string CallbackMember, int? SortValue = null);

/// <summary>Six accepted producer roles. This matches managed route identity only; native ownership is proved by the provider.</summary>
public static class Ui6ConsumerRoutePolicy
{
    static readonly Ui6ConsumerRoute[] routes = [
        new("UI6-FILTER-CheckBox","gc.filterpopup","filter_rarity_heading","Gc.CheckBoxFilterGroup","Setup"),
        new("UI6-FILTER-Toggle","gc.filterpopup","filter_rarity_heading","Gc.ToggleFilterGroup","Setup"),
        new("UI6-SORT-UI4-M015","gc.sortpopup","equipment_sort_rarity_option","Gc.SortPopup","Setup",0),
        new("UI6-SORT-UI4-M019","gc.sortpopup","equipment_sort_acquisition_option","Gc.SortPopup","Setup",10),
        new("UI6-UI4-M006-OWNED_CATEGORY_FRAME_TEXT","gc.shop.shoptopview","shop_currency_frame_title","Gc.Shop.ShopCategoryScrollCell","Setup"),
        new("UI6-UI4-M006-OWNED_CATEGORY_SECTION_TITLE","gc.shop.shoptopview","shop_currency_section_title","Gc.Shop.ShopCategoryScrollCell","Setup")
    ];
    public static Ui6ConsumerRoute? Find(string identity) => routes.SingleOrDefault(r => r.Identity == identity);
    // Renderer classification only. Actual native callback/source proof is supplied by GeneralUiProvider.
    public static bool UsesScopedConsumerRenderer(UiBinding binding) => binding.AdapterId == "core.current-model.v1" && binding.Selector.Type == "model"
        && routes.Any(r => binding.Selector.ScopeKey == r.Scope && binding.Selector.SlotKey == r.Slot
            && binding.Selector.Lifetime == (r.SortValue.HasValue ? "callback" : "owner") && binding.SourceIdentity.Kind == "current-model"
            && binding.SourceIdentity.IdentityJson == JsonSerializer.Serialize(r.Identity));
    public static bool Matches(UiBinding binding, string identity, string callbackType, string callbackMember, int? sortValue = null)
    {
        var route = Find(identity);
        return route != null && binding.Enabled && binding.AdapterId == "core.current-model.v1"
            && binding.SourceIdentity.Kind == "current-model" && binding.SourceIdentity.IdentityJson == JsonSerializer.Serialize(identity)
            && binding.Selector.ScopeKey == route.Scope && binding.Selector.SlotKey == route.Slot && binding.Selector.Type == "model"
            && binding.Selector.Lifetime == (route.SortValue.HasValue ? "callback" : "owner")
            && callbackType == route.CallbackType && callbackMember == route.CallbackMember && sortValue == route.SortValue;
    }
}
