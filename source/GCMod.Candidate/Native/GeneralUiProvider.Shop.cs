#nullable enable
using System.Text.Json;
using TMPro;

namespace GCMod.Candidate.Native;

public sealed partial class GeneralUiProvider
{
    sealed class ShopHeaderReceipt
    {
        public Gc.Shop.ShopTopController Page = null!;
        public Gc.HeaderTopController Header = null!;
        public TextMeshProUGUI Text = null!;
        public int PageInstance, HeaderInstance, TextInstance, Scene, StartedFrame;
        public long Model, Controller, Target, Lifetime;
    }
    readonly Dictionary<long, ShopHeaderReceipt> shopHeaders = new();

    void CaptureShopHeader(Gc.Shop.ShopTopController page, Gc.HeaderTopController header, NativeHookContext callback)
    {
        gate.Require();
        if (!Enabled || !Alive(page) || !Alive(header) || !Alive(header._subSceneTitleText) ||
            Ptr(page._viewData) == 0 || !Alive(page.Controller) || shopHeaders.Count >= 8 && !shopHeaders.ContainsKey(Ptr(page))) return;
        Queue(page);
        if (!owners.TryGetValue(Ptr(page), out var owner)) return;
        var text = header._subSceneTitleText;
        var receipt = new ShopHeaderReceipt { Page = page, Header = header, Text = text,
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
            Ptr(receipt.Page.Controller) == receipt.Controller && Alive(receipt.Header) &&
            receipt.Header.GetInstanceID() == receipt.HeaderInstance && Alive(receipt.Text) &&
            receipt.Text.GetInstanceID() == receipt.TextInstance && Ptr(receipt.Header._subSceneTitleText) == receipt.Target &&
            Ptr(receipt.Text) == receipt.Target && owners.TryGetValue(Ptr(receipt.Page), out var owner) &&
            owner.Lifetime == receipt.Lifetime && Current(owner);
    }

    void ApplyShopHeader(ShopHeaderReceipt receipt, NativeHookContext? callback)
    {
        gate.Require();
        if (!Enabled || !ShopHeaderSourceCurrent(receipt) || !ShopSceneCurrent(receipt.Page, receipt.Header)) return;
        bool CurrentHeader() => ShopHeaderSourceCurrent(receipt) && ShopSceneCurrent(receipt.Page, receipt.Header);
        string HeaderInput() => JsonSerializer.Serialize(new { header = Ptr(receipt.Header),
            target = Ptr(receipt.Header._subSceneTitleText), controls = Controls(receipt.Header) });
        ApplyDirect(receipt.Page, receipt.Text, "gc.shop.shoptopcontroller", "shop_title", receipt.Text.text,
            CurrentHeader, callback, model: true, readInput: HeaderInput, rebound: () => !CurrentHeader());
    }

    void RetryShopHeader(Gc.Shop.ShopTopController page, NativeHookContext callback)
    {
        gate.Require();
        if (!Enabled || !shopHeaders.TryGetValue(Ptr(page), out var receipt)) return;
        if (!ShopHeaderSourceCurrent(receipt)) { shopHeaders.Remove(Ptr(page)); return; }
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
