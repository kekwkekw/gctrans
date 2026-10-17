#nullable enable
using System.Text.Json;
using TMPro;
using GCMod.Nextgen;

namespace GCMod.Candidate.Native;

// Primitive identity policy used by the actual native adapter. CPU input is an explicit
// mock graph; these values alone never claim native class, callback ABI or display proof.
public sealed record ShopHeaderGraphIdentity(long Page, long View, long PageView, long Model, long ViewModel,
    long Controller, long Header, long CurrentHeader, long Target, long HeaderTarget, int CurrentTab, int CallbackTab);
public static class ShopHeaderCurrentTabPolicy
{
    public static bool Matches(ShopHeaderGraphIdentity value) =>
        value.Page != 0 && value.View != 0 && value.PageView == value.View && value.Model != 0 && value.ViewModel == value.Model &&
        value.Controller != 0 && value.Header != 0 && value.CurrentHeader == value.Header && value.Target != 0 &&
        value.HeaderTarget == value.Target && value.Target != value.Header && value.CurrentTab == value.CallbackTab && value.CurrentTab >= 0;
    public static bool SelectedPage(long page, int current, IReadOnlyList<long> created) =>
        page != 0 && created.Count is > 0 and <= 32 && current >= 0 && current < created.Count &&
        created[current] == page && created.Count(x => x == page) == 1;
}

public sealed partial class GeneralUiProvider
{
    sealed class ShopHeaderReceipt
    {
        public Gc.Shop.ShopTopController Page = null!;
        public Gc.HeaderTopController Header = null!;
        public TextMeshProUGUI Text = null!;
        public Gc.Shop.ShopTopView View = null!;
        public int PageInstance, HeaderInstance, TextInstance, Scene, StartedFrame;
        public int ViewInstance;
        public long Model, Controller, Target, Lifetime;
    }
    readonly Dictionary<long, ShopHeaderReceipt> shopHeaders = new();
    readonly HashSet<string> shopHeaderReasons = new(StringComparer.Ordinal);
    void ShopHeaderReason(string reason)
    {
        if (shopHeaderReasons.Count < 32 && shopHeaderReasons.Add(reason)) log.Record("SHOP_HEADER_"+reason);
    }

    void CaptureShopHeader(Gc.Shop.ShopTopController page, Gc.HeaderTopController header, NativeHookContext callback)
    {
        gate.Require();
        if (!Enabled || !Alive(page) || !Alive(page._view) || !Alive(header) || !Alive(header._subSceneTitleText) ||
            Ptr(page._viewData) == 0 || !Alive(page.Controller) || shopHeaders.Count >= 8 && !shopHeaders.ContainsKey(Ptr(page))) return;
        if (!Exact(page,"Gc.Shop.ShopTopController") || !Exact(page._view,"Gc.Shop.ShopTopView") ||
            Ptr(page._view._viewData) != Ptr(page._viewData) || !page._view.transform.IsChildOf(page.transform) ||
            !Exact(header, "Gc.HeaderTopController") || !Exact(header._subSceneTitleText, "TMPro.TextMeshProUGUI") ||
            !header._subSceneTitleText.transform.IsChildOf(header.transform))
        { ShopHeaderReason("TYPED_PAGE_HEADER_TARGET_MISMATCH"); return; }
        Queue(page);
        if (!owners.TryGetValue(Ptr(page), out var owner)) return;
        var text = header._subSceneTitleText;
        var receipt = new ShopHeaderReceipt { Page = page, Header = header, Text = text, View = page._view, ViewInstance = page._view.GetInstanceID(),
            PageInstance = page.GetInstanceID(), HeaderInstance = header.GetInstanceID(), TextInstance = text.GetInstanceID(),
            Scene = page.gameObject.scene.handle, StartedFrame = frame, Model = Ptr(page._viewData),
            Controller = Ptr(page.Controller), Target = Ptr(text), Lifetime = owner.Lifetime };
        shopHeaders[Ptr(page)] = receipt;
        ApplyShopHeader(receipt, callback);
    }

    bool ShopHeaderSourceCurrent(ShopHeaderReceipt receipt)
    {
        gate.Require();
        return Alive(receipt.Page) && receipt.Page.GetInstanceID() == receipt.PageInstance &&
            receipt.Page.gameObject.scene.handle == receipt.Scene && Ptr(receipt.Page._viewData) == receipt.Model &&
            Alive(receipt.View) && receipt.View.GetInstanceID() == receipt.ViewInstance && Ptr(receipt.Page._view) == Ptr(receipt.View) &&
            Ptr(receipt.View._viewData) == receipt.Model && receipt.View.transform.IsChildOf(receipt.Page.transform) &&
            Ptr(receipt.Page.Controller) == receipt.Controller && Alive(receipt.Header) &&
            receipt.Header.GetInstanceID() == receipt.HeaderInstance && Alive(receipt.Text) &&
            receipt.Text.GetInstanceID() == receipt.TextInstance && Ptr(receipt.Header._subSceneTitleText) == receipt.Target &&
            receipt.Text.transform.IsChildOf(receipt.Header.transform) &&
            Ptr(receipt.Text) == receipt.Target && owners.TryGetValue(Ptr(receipt.Page), out var owner) &&
            owner.Lifetime == receipt.Lifetime && Current(owner);
    }

    void ApplyShopHeader(ShopHeaderReceipt receipt, NativeHookContext? callback)
    {
        gate.Require();
        if (!Enabled || !ShopHeaderSourceCurrent(receipt)) { ShopHeaderReason("OWNER_VIEW_MODEL_TARGET_NOT_CURRENT"); return; }
        bool SelectedPage()
        {
            var controller=receipt.Page.Controller;var created=controller?.createdScene;
            return Alive(controller)&&created!=null&&created.Length<=32&&
                ShopHeaderCurrentTabPolicy.SelectedPage(Ptr(receipt.Page),controller!.currentSubSceneIndex,created.Select(x=>Ptr(x)).ToArray());
        }
        if (!receipt.Page.gameObject.activeInHierarchy || !ShopSceneCurrent(receipt.Page, receipt.Header) || !SelectedPage())
        { ShopHeaderReason("CURRENT_SCENE_WAIT"); return; }
        bool CurrentHeader() => ShopHeaderSourceCurrent(receipt) && receipt.Page.gameObject.activeInHierarchy &&
            ShopSceneCurrent(receipt.Page, receipt.Header) && SelectedPage();
        string HeaderInput() => JsonSerializer.Serialize(new { header = Ptr(receipt.Header),
            target = Ptr(receipt.Header._subSceneTitleText), controls = Controls(receipt.Header) });
        ApplyDirect(receipt.Page, receipt.Text, "gc.shop.shoptopcontroller", "shop_title", receipt.Text.text,
            CurrentHeader, callback, model: true, readInput: HeaderInput, rebound: () => !CurrentHeader());
        ShopHeaderReason("EXACT_CURRENT_HEADER_APPLY_ENTERED");
    }

    void RetryShopHeader(Gc.Shop.ShopTopController page, NativeHookContext callback)
    {
        gate.Require();
        if (!Enabled || !Alive(page)) return;
        if (!shopHeaders.TryGetValue(Ptr(page), out var receipt) || !ShopHeaderSourceCurrent(receipt))
        {
            shopHeaders.Remove(Ptr(page));
            // The existing completed natural tab callback is a separate current producer.
            // No receipt or async closure is fabricated, and no callback pointer is retained.
            if (!callback.Active || callback.IsPrefix || callback.Exception != null ||
                callback.Instance is not Gc.Shop.ShopTopView view || !Alive(view) ||
                callback.Method.DeclaringType != typeof(Gc.Shop.ShopTopView) || callback.Method.Name != "SetupCurrentTabView" ||
                callback.Args.Length != 1 || callback.Args[0] is not int tab)
            { ShopHeaderReason("REAL_CURRENT_TAB_CALLBACK_REQUIRED"); return; }
            var game=GameSystem._Instance_k__BackingField;var header=game?._header?._TopController_k__BackingField;
            if (!Alive(game) || !Alive(header) || !Alive(page._view) || !Alive(header!._subSceneTitleText) || !Alive(page.Controller))
            { ShopHeaderReason("CURRENT_HEADER_OR_PAGE_UNAVAILABLE"); return; }
            var currentHeader=header!;
            var graph=new ShopHeaderGraphIdentity(Ptr(page),Ptr(view),Ptr(page._view),Ptr(page._viewData),Ptr(view._viewData),
                Ptr(page.Controller),Ptr(currentHeader),Ptr(game!._header!._TopController_k__BackingField),
                Ptr(currentHeader._subSceneTitleText),Ptr(currentHeader._subSceneTitleText),view._viewData?.CurrentTabIndex??-1,tab);
            if (!ShopHeaderCurrentTabPolicy.Matches(graph) || !view.transform.IsChildOf(page.transform))
            { ShopHeaderReason("CURRENT_TAB_VIEW_MODEL_IDENTITY_MISMATCH"); return; }
            ShopHeaderReason("NATURAL_CURRENT_TAB_CAPTURED");
            CaptureShopHeader(page,currentHeader,callback);
            return;
        }
        receipt.StartedFrame = frame; // Only this qualifying natural current-tab callback opens another finite clock.
        ApplyShopHeader(receipt, callback);
    }

    void TickShopHeaders()
    {
        gate.Require();
        if (!Enabled) return;
        foreach (var pair in shopHeaders.ToArray())
        {
            try
            {
                var receipt = pair.Value;
                if (!ShopHeaderSourceCurrent(receipt)) { Reclaim(receipt.Page); shopHeaders.Remove(pair.Key); continue; }
                int age = frame - receipt.StartedFrame;
                if (age is 1 or 3 or 8 or 20 or 60 or 90) ApplyShopHeader(receipt, null);
                if (age > 180 && !targets.ContainsKey(pair.Key)) shopHeaders.Remove(pair.Key);
            }
            catch { log.Record("GENERAL_SHOP_HEADER_READ_ISOLATED"); }
        }
    }
}
