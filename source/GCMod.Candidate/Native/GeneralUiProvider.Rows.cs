#nullable enable
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using GCMod.Nextgen;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GCMod.Candidate.Native;
public sealed partial class GeneralUiProvider
{
    sealed class RowReceipt
    {
        public Component Cell = null!;
        public int Instance, Scene, StartedFrame;
        public long Data, ParentModel, Collection;
        public Component Parent = null!;
        public Func<bool> Current = null!;
        public Action<NativeHookContext?> Apply = null!;
    }
    sealed class ScrollReceipt
    {
        public Gc.Shop.ExchangeShopProductsView View = null!;
        public int Instance, Scene, Reads;
        public long Model, Collection, Started, LastEvent;
        public bool Batch, Exhausted;
        public UnityEngine.Events.UnityAction<Vector2>? Listener;
        public ScrollRect.ScrollRectEvent? Event;
        public long EventPointer;
    }
    readonly Dictionary<long, RowReceipt> rowReceipts = new();
    readonly Dictionary<long, ScrollReceipt> scrollReceipts = new();
    static long RowStamp() => Stopwatch.GetTimestamp();
    static double RowAge(long stamp) => (RowStamp() - stamp) / (double)Stopwatch.Frequency;

    void InitializeRows()
    {
        Hook(typeof(Gc.Shop.ExchangeShopScrollView), "_Init_b__10_0", 2, c =>
        {
            var scroller = (Gc.Shop.ExchangeShopScrollView)c.Instance!;
            var view = Parent<Gc.Shop.ExchangeShopProductsView>(scroller);
            var cell = c.Args[1] as EnhancedUI.EnhancedScroller.EnhancedScrollerCellView;
            if (!Alive(view) || Ptr(view!._scrollView) != Ptr(scroller) || Ptr(scroller._scroller) != Ptr((EnhancedUI.EnhancedScroller.EnhancedScroller)c.Args[0]) ||
                !Alive(cell) || !Alive(scroller._content) || !cell!.transform.IsChildOf(scroller._content)) return;
            var receipt = EnsureScrollReceipt(view);
            if (receipt != null) ArmScrollBatch(receipt);
        });
    }

    void TickRows()
    {
        gate.Require();
        foreach (var pair in rowReceipts.ToArray())
        {
            var row = pair.Value;
            try
            {
                if (!row.Current()) { Reclaim(row.Cell); rowReceipts.Remove(pair.Key); continue; }
                int age = frame - row.StartedFrame;
                if (age is 1 or 3 or 8 or 20 or 60 or 90) row.Apply(null);
                if (age > 180 && !targets.ContainsKey(pair.Key)) rowReceipts.Remove(pair.Key);
            }
            catch { log.Record("GENERAL_ROW_READ_ISOLATED"); }
        }
        foreach (var pair in scrollReceipts.ToArray())
        {
            var receipt = pair.Value;
            try
            {
                if (!ScrollCurrent(receipt))
                {
                    if (RemoveScrollListener(receipt)) scrollReceipts.Remove(pair.Key);
                    continue;
                }
                if (receipt.Listener != null && Ptr(receipt.View._scrollView._scrollRect.onValueChanged) != receipt.EventPointer)
                {
                    if (RemoveScrollListener(receipt)) InstallScrollListener(receipt);
                }
                if (!receipt.Batch) continue;
                double age = RowAge(receipt.Started);
                if (age > 2 || receipt.Reads >= 4) { receipt.Batch = false; receipt.Exhausted = true; continue; }
                if (age < new[] { 0d, .25, .75, 1.5 }[receipt.Reads]) continue;
                receipt.Reads++;
                var cells = receipt.View._scrollView._content.GetComponentsInChildren<Gc.Shop.ExchangeShopScrollCell>(false);
                Require(cells.Length <= 128, "CURRENT_VISIBLE_BADGE_CELLS_BOUND");
                foreach (var cell in cells)
                {
                    if (!cell.gameObject.activeInHierarchy || !CurrentExchangeRow(cell)) continue;
                    if (!rowReceipts.TryGetValue(Ptr(cell), out var row) || row.Data != Ptr(cell._data))
                        CaptureExchangeRow(cell, cell._data!, null);
                    else row.Apply(null);
                    RecheckBadgeGeometry(cell);
                }
                log.Record("GENERAL_CURRENT_VISIBILITY_BATCH_READ");
            }
            catch { log.Record("GENERAL_VISIBILITY_READ_ISOLATED"); }
        }
    }
    bool CurrentExchangeRow(Gc.Shop.ExchangeShopScrollCell cell)
    {
        if (!Alive(cell) || cell._data == null) return false;
        var view = Parent<Gc.Shop.ExchangeShopProductsView>(cell); var shop = Parent<Gc.Shop.ShopTopController>(cell);
        var scroll = view?._scrollView; var content = scroll?._content;
        if (view == null || shop == null || scroll == null || content == null || !Alive(view) || !Alive(shop) || view._data?.CellDataList == null ||
            Ptr(shop._view?._exchangeShopView?._productView) != Ptr(view) || !Alive(scroll) || !Alive(content) ||
            !cell.transform.IsChildOf(content)) return false;
        var list = view._data.CellDataList;
        return list.Count <= 512 && list.ToArray().Count(x => Ptr(x) == Ptr(cell._data)) == 1;
    }
    void CaptureExchangeRow(Gc.Shop.ExchangeShopScrollCell cell, Gc.Shop.ExchangeShopScrollCellData data, NativeHookContext? callback)
    {
        gate.Require();
        if (data == null || Ptr(cell._data) != Ptr(data) || !CurrentExchangeRow(cell) || !Alive(cell._exchangeBadge)) return;
        var view = Parent<Gc.Shop.ExchangeShopProductsView>(cell)!; var shop = Parent<Gc.Shop.ShopTopController>(cell)!;
        var list = view._data.CellDataList; var texts = cell._exchangeBadge.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts.Length != 1 || rowReceipts.Count >= 64 && !rowReceipts.ContainsKey(Ptr(cell))) return;
        var text = texts[0]; long model = Ptr(view._data), collection = Ptr(list), badge = Ptr(cell._exchangeBadge), target = Ptr(text);
        int instance = cell.GetInstanceID(), viewInstance = view.GetInstanceID(), shopInstance = shop.GetInstanceID();
        bool CurrentRow() => Alive(cell) && cell.GetInstanceID() == instance && Ptr(cell._data) == Ptr(data) &&
            Alive(view) && view.GetInstanceID() == viewInstance && Alive(shop) && shop.GetInstanceID() == shopInstance &&
            Ptr(Parent<Gc.Shop.ExchangeShopProductsView>(cell)) == Ptr(view) && Ptr(Parent<Gc.Shop.ShopTopController>(cell)) == Ptr(shop) &&
            Ptr(shop._view?._exchangeShopView?._productView) == Ptr(view) && Ptr(view._data) == model && Ptr(view._data?.CellDataList) == collection &&
            CurrentExchangeRow(cell) && Ptr(cell._exchangeBadge) == badge && Alive(text) && Ptr(text) == target &&
            text.transform.IsChildOf(cell._exchangeBadge.transform) && cell._exchangeBadge.GetComponentsInChildren<TextMeshProUGUI>(true).Length == 1;
        void ApplyRow(NativeHookContext? c) => ApplyDirect(cell, text, "gc.shop.exchangeshopscrollcell", "exchange_available_badge", text.text,
            CurrentRow, c, model: true, cell: true, readInput: () => ReadNumericSnapshot(cell), rebound: () => !CurrentRow());
        rowReceipts[Ptr(cell)] = new() { Cell = cell, Instance = instance, Scene = cell.gameObject.scene.handle, StartedFrame = frame,
            Data = Ptr(data), Parent = view, ParentModel = model, Collection = collection, Current = CurrentRow, Apply = ApplyRow };
        ApplyRow(callback);
        var scroll = EnsureScrollReceipt(view); if (scroll != null) ArmScrollBatch(scroll);
    }
    void CaptureConsumeRow(Gc.ExchangeLayerPieceConsumeScrollCell cell, Gc.ExchangeLayerPieceConsumeScrollCellData data, NativeHookContext? callback)
    {
        gate.Require();
        var popup = Parent<Gc.ExchangeLayerPiecePopup>(cell);
        if (data == null || Ptr(cell._cellData) != Ptr(data) || !Alive(popup) || popup!._data?.CellDataList == null ||
            rowReceipts.Count >= 64 && !rowReceipts.ContainsKey(Ptr(cell))) return;
        var list = popup._data.CellDataList;
        if (list.Count > 128 || list.ToArray().Count(x => Ptr(x) == Ptr(data)) != 1) return;
        int instance = cell.GetInstanceID(), popupInstance = popup.GetInstanceID();
        long model = Ptr(popup._data), collection = Ptr(list), amount = Ptr(cell._amount), slider = Ptr(cell._slider);
        bool CurrentRow() => Alive(cell) && cell.GetInstanceID() == instance && Ptr(cell._cellData) == Ptr(data) &&
            Alive(popup) && popup.GetInstanceID() == popupInstance && Ptr(Parent<Gc.ExchangeLayerPiecePopup>(cell)) == Ptr(popup) &&
            Ptr(popup._data) == model && Ptr(popup._data?.CellDataList) == collection && popup._data!.CellDataList.Count <= 128 &&
            popup._data.CellDataList.ToArray().Count(x => Ptr(x) == Ptr(data)) == 1 && Ptr(cell._amount) == amount && Ptr(cell._slider) == slider;
        void ApplyRow(NativeHookContext? c)
        {
            if (!CurrentRow()) return;
            if (c?.Active == true || !owners.ContainsKey(Ptr(cell))) Queue(cell);
            if (!owners.TryGetValue(Ptr(cell), out var owner)) return;
            foreach (var binding in host.Snapshot.Bindings.Values.Where(b => b.Enabled && b.Selector.ScopeKey == "gc.exchangelayerpiececonsumescrollcell"))
                foreach (var leaf in ExactPaths(cell.transform, binding.Selector.Segments))
                {
                    var texts = leaf.GetComponents<TextMeshProUGUI>(); if (texts.Length != 1) continue;
                    var text = texts[0]; long target = Ptr(text);
                    Apply(owner, text, binding, () => CurrentRow() && Alive(text) && Ptr(text) == target && text.transform.IsChildOf(cell.transform), c,
                        model: true, cell: true, rebound: () => !CurrentRow(), readInput: () => ReadNumericSnapshot(cell));
                }
        }
        rowReceipts[Ptr(cell)] = new() { Cell = cell, Instance = instance, Scene = cell.gameObject.scene.handle, StartedFrame = frame,
            Data = Ptr(data), Parent = popup, ParentModel = model, Collection = collection, Current = CurrentRow, Apply = ApplyRow };
        ApplyRow(callback);
    }

    // Read the exact recovered row identity fields and actual rendered numeric/control values.
    // The text writer reads this before and after its setter; this is never a supplied bool.
    string ReadNumericSnapshot(Component owner)
    {
        gate.Require();
        object? model = null;
        if (owner is Gc.Shop.ExchangeShopScrollCell exchange)
            model = new { row = Ptr(exchange._data), productId = exchange._data?.ProductId, product = Ptr(exchange._data?.ProductData),
                badge = Ptr(exchange._exchangeBadge), view = Ptr(Parent<Gc.Shop.ExchangeShopProductsView>(exchange)),
                collection = Ptr(Parent<Gc.Shop.ExchangeShopProductsView>(exchange)?._data?.CellDataList) };
        else if (owner is Gc.ExchangeLayerPieceConsumeScrollCell consume)
            model = new { row = Ptr(consume._cellData), productId = consume._cellData?.ShopProductId, itemId = consume._cellData?.ItemId,
                itemType = consume._cellData?.ItemType, amount = Ptr(consume._amount), slider = Ptr(consume._slider) };
        else if (owner is Gc.ExchangeLayerPiecePopup popup)
        {
            var list = popup._data?.CellDataList;
            Require(list != null && list.Count <= 128, "EXCHANGE_NUMERIC_MODEL_BOUND");
            model = new { model = Ptr(popup._data), collection = Ptr(list), rows = list!.ToArray().Select(x => new
                { ptr = Ptr(x), x.ShopProductId, x.ItemId, x.ItemType }).ToArray() };
        }
        var texts = owner.GetComponentsInChildren<TextMeshProUGUI>(true); var sliders = owner.GetComponentsInChildren<Slider>(true);
        Require(texts.Length <= 128 && sliders.Length <= 16, "EXCHANGE_NUMERIC_DISPLAY_BOUND");
        var numeric = texts.Where(t => t.text != null && t.text.Length <= 4096 &&
            Regex.IsMatch(t.text, @"^\s*[\p{N}\s.,＋+−\-/:％%×]+\s*$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(30)))
            .Select(t => new { pointer = Ptr(t), instance = t.GetInstanceID(), text = t.text }).ToArray();
        return JsonSerializer.Serialize(new { model, numeric, sliders = sliders.Select(s => new
            { pointer = Ptr(s), s.value, s.minValue, s.maxValue, callback = Ptr(s.onValueChanged) }).ToArray() });
    }

    bool ScrollCurrent(ScrollReceipt r)
    {
        var view = r.View; var shop = Alive(view) ? Parent<Gc.Shop.ShopTopController>(view) : null;
        return Alive(view) && view.GetInstanceID() == r.Instance && view.gameObject.scene.handle == r.Scene && view.gameObject.activeInHierarchy &&
            Ptr(view._data) == r.Model && Ptr(view._data?.CellDataList) == r.Collection && Alive(shop) &&
            Ptr(shop!._view?._exchangeShopView?._productView) == Ptr(view) && Alive(view._scrollView?._scrollRect) && Alive(view._scrollView?._content);
    }
    ScrollReceipt? EnsureScrollReceipt(Gc.Shop.ExchangeShopProductsView view)
    {
        if (!Alive(view) || view._data?.CellDataList == null || !Alive(view._scrollView?._scrollRect)) return null;
        long key = Ptr(view);
        if (scrollReceipts.TryGetValue(key, out var old))
        {
            if (ScrollCurrent(old)) { InstallScrollListener(old); return old; }
            if (!RemoveScrollListener(old)) return null;
            scrollReceipts.Remove(key);
        }
        if (scrollReceipts.Count >= 8) return null;
        var receipt = new ScrollReceipt { View = view, Instance = view.GetInstanceID(), Scene = view.gameObject.scene.handle,
            Model = Ptr(view._data), Collection = Ptr(view._data.CellDataList) };
        scrollReceipts[key] = receipt; InstallScrollListener(receipt); return receipt;
    }
    void ArmScrollBatch(ScrollReceipt r)
    {
        if (!ScrollCurrent(r) || r.Batch) return;
        r.Batch = true; r.Exhausted = false; r.Started = RowStamp(); r.Reads = 0;
    }
    void InstallScrollListener(ScrollReceipt r)
    {
        gate.Require();
        if (!Enabled) return;
        if (!ScrollCurrent(r) || r.Listener != null) return;
        var scroll = r.View._scrollView;
        if (Ptr(scroll._scroller.cellViewVisibilityChanged) != 0) return;
        var e = scroll._scrollRect.onValueChanged; if (Ptr(e) == 0) return;
        r.Event = e; r.EventPointer = Ptr(e);
        r.Listener = DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<Vector2>>((Action<Vector2>)(_ =>
        {
            if (!gate.CanAccess || !Enabled) return;
            try
            {
                if (!ScrollCurrent(r) || Ptr(r.View._scrollView._scrollRect.onValueChanged) != r.EventPointer) return;
                double quiet = r.LastEvent == 0 ? double.MaxValue : RowAge(r.LastEvent); r.LastEvent = RowStamp();
                if (!r.Batch && (!r.Exhausted || quiet >= .15)) ArmScrollBatch(r);
            }
            catch { log.Record("GENERAL_OWN_SCROLL_CALLBACK_ISOLATED"); }
        }));
        e.AddListener(r.Listener); log.Record("GENERAL_OWN_SCROLL_OBSERVER_ADDED");
    }
    bool RemoveScrollListener(ScrollReceipt r)
    {
        gate.Require();
        if (r.Listener == null) return true;
        try
        {
            r.Event?.RemoveListener(r.Listener); r.Listener = null; r.Event = null;
            log.Record("GENERAL_OWN_SCROLL_OBSERVER_REMOVED"); return true;
        }
        catch { log.Record("GENERAL_OWN_SCROLL_REMOVE_RETRYABLE"); return false; }
    }
    void RecheckBadgeGeometry(Gc.Shop.ExchangeShopScrollCell cell)
    {
        if (!targets.TryGetValue(Ptr(cell), out var ids)) return;
        foreach (var id in ids.ToArray())
            if (geometryOwned.TryGetValue(id, out var ticket) && ticket.Policy == GeometryPolicy.BadgeIntrinsic)
                ticket.Settled = 0; // Scoped visibility event reopens two current intrinsic checks only.
    }
    bool RestoreRows()
    {
        gate.Require(); bool complete = true;
        foreach (var pair in scrollReceipts.ToArray())
        {
            if (RemoveScrollListener(pair.Value)) scrollReceipts.Remove(pair.Key); else complete = false;
        }
        if (complete) rowReceipts.Clear();
        return complete;
    }
    void SelectedRows() { gate.Require(); rowReceipts.Clear(); }
}
