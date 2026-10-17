#nullable enable
using GCMod.Nextgen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GCMod.Candidate.Native;

public sealed partial class GeneralUiProvider
{
    sealed record Ui6ProducerStamp(string Type, string Member, long NaturalCallbackLifetime);
    sealed record Ui6OwnerPending(Component Owner, TextMeshProUGUI Text, string Identity, string Source, Func<bool> Current,
        Ui6ProducerStamp Producer, int OwnerInstance, int TargetInstance, long OwnerLifetime, int Started, Func<string>? ReadInput,
        Action<Owner>? PreparedOwnerApply = null);
    readonly Dictionary<(long Owner, long Target, string Identity), Ui6OwnerPending> ui6Pending = new();
    readonly HashSet<string> ui6SourceReports = new(StringComparer.Ordinal);
    void ReportUi6Source(string identity, Component? owner, string reason)
    {
        if (!gate.CanAccess) return;
        long lifetime = Alive(owner) && owners.TryGetValue(Ptr(owner), out var current) ? current.Lifetime : 0;
        string code = Ui6SourceDiagnosticCodes.Create(identity,lifetime,reason);
        if (ui6SourceReports.Count < 256 && ui6SourceReports.Add(code)) log.Record(code);
    }
    bool TryUi6TextTopology(TextMeshProUGUI text, out string topology)
    {
        topology = "";
        if (!gate.CanAccess || !Alive(text)) return false;
        var localizers = text.GetComponents<Gc.Localizer.TextLocalizer>();
        if (localizers.Length > 1) return false;
        var localizer = localizers.SingleOrDefault();
        if (Alive(localizer) && Ptr(localizer!.text) != Ptr(text)) return false;
        topology = System.Text.Json.JsonSerializer.Serialize(new { pointer = Ptr(localizer), instance = Alive(localizer) ? localizer!.GetInstanceID() : 0,
            kind = Alive(localizer) ? (int)localizer!.type : 0, key = Alive(localizer) ? localizer!.textConfigKey ?? "" : "" });
        return true;
    }
    // The receipt-less modern path keeps the original six accepted typed producer roles.
    // Source/value/current physical identity still comes from each natural callback.
    void ApplyUi6Consumer(Component owner, TextMeshProUGUI text, string identity, string source,
        Func<bool> current, NativeHookContext callback, int? sortValue = null, Func<string>? readInput = null)
    {
        gate.Require();
        if (!callback.Active || callback.IsPrefix || callback.Lifetime <= 0 || !Alive(owner) || !Alive(text)) return;
        if (!TryUi6TextTopology(text, out string topology)) return;
        bool CurrentText() => current() && TryUi6TextTopology(text, out var now) && now == topology;
        var producer = new Ui6ProducerStamp(callback.Method.DeclaringType?.FullName ?? "", callback.Method.Name, callback.Lifetime);
        var route = Ui6ConsumerRoutePolicy.Find(identity);
        if (route == null || route.CallbackType != producer.Type || route.CallbackMember != producer.Member) return;
        if (!host.Snapshot.Bindings.Values.Any(b=>Ui6ConsumerRoutePolicy.Matches(b,identity,producer.Type,producer.Member,sortValue)&&b.SourceIdentity.ExactJp==source))
        { string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source)))[..12];
            ReportUi6Source(identity, owner, $"NO_ENABLED_EXACT_SOURCE_BINDING_L{source.Length}_H{hash}"); return; }
        Queue(owner);
        if (!owners.TryGetValue(Ptr(owner), out var generation)) return;
        var pendingKey = (Ptr(owner), Ptr(text), identity);
        if (route.SortValue == null && (ui6Pending.Count < 64 || ui6Pending.ContainsKey(pendingKey)))
            ui6Pending[pendingKey] = new(owner, text, identity, source, CurrentText, producer,
                owner.GetInstanceID(), text.GetInstanceID(), generation.Lifetime, frame, readInput);
        ApplyUi6Prepared(generation, text, identity, source, CurrentText, producer, callback, sortValue, readInput);
    }
    void ApplyUi6Prepared(Owner generation, TextMeshProUGUI text, string identity, string source, Func<bool> current,
        Ui6ProducerStamp producer, NativeHookContext? callback, int? sortValue, Func<string>? readInput)
    {
        gate.Require();
        if (!Current(generation) || !Alive(text) || !current()) return;
        var route = Ui6ConsumerRoutePolicy.Find(identity);
        if (route == null || route.SortValue.HasValue && callback?.Active != true) return;
        foreach (var binding in host.Snapshot.Bindings.Values.Where(b => Ui6ConsumerRoutePolicy.Matches(b, identity, producer.Type, producer.Member, sortValue)
            && b.SourceIdentity.ExactJp == source))
        {
            if (!binding.Selector.Segments.IsEmpty)
            {
                var actualPath = UnityNative.Nodes(text.transform, generation.Value.transform);
                if (!actualPath.SequenceEqual(binding.Selector.Segments))
                {
                    string pathHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\0",actualPath))))[..12];
                    ReportUi6Source(identity, generation.Value, $"EXACT_PATH_MISMATCH_C{actualPath.Length}_H{pathHash}"); continue;
                }
            }
            Apply(generation, text, binding, current, callback, model: true, readInput: readInput, rebound: () => !current());
        }
    }
    void TickUi6Consumers()
    {
        gate.Require();
        foreach (var entry in ui6Pending.ToArray())
        {
            var pending = entry.Value; int age = frame - pending.Started;
            if (!Alive(pending.Owner) || !Alive(pending.Text) || pending.Owner.GetInstanceID() != pending.OwnerInstance
                || pending.Text.GetInstanceID() != pending.TargetInstance || !owners.TryGetValue(Ptr(pending.Owner), out var owner)
                || owner.Lifetime != pending.OwnerLifetime || age > 120)
            { ui6Pending.Remove(entry.Key); continue; }
            if (age is not (1 or 3 or 8 or 20 or 60 or 90)) continue;
            // This is an owner-lifetime revalidation, not callback replay. CallbackActive remains false.
            try { if (pending.PreparedOwnerApply != null) pending.PreparedOwnerApply(owner);
                else ApplyUi6Prepared(owner, pending.Text, pending.Identity, pending.Source, pending.Current, pending.Producer, null, null, pending.ReadInput); }
            catch { ui6Pending.Remove(entry.Key); log.Record("UI6_OWNER_REVALIDATION_FAILURE_ISOLATED"); }
        }
    }
    sealed record Ui6FilterTicket(int PopupInstance, int GroupInstance, int TargetInstance, long Setting, string Source, string Identity);
    bool Ui6FilterCurrent(Component group, Gc.FilterPopup popup, TextMeshProUGUI text, Ui6FilterTicket ticket)
        => Ui6FilterCurrent(group, popup, text, ticket, out _);
    bool Ui6FilterCurrent(Component group, Gc.FilterPopup popup, TextMeshProUGUI text, Ui6FilterTicket ticket, out int domain)
    {
        domain = -1;
        bool No(string reason) { ReportUi6Source(ticket.Identity, popup, reason); return false; }
        if (!gate.CanAccess || !Alive(group) || !Alive(popup) || !Alive(text) || group.GetInstanceID()!=ticket.GroupInstance
            || popup.GetInstanceID()!=ticket.PopupInstance || text.GetInstanceID()!=ticket.TargetInstance) return No("OWNER_GROUP_TARGET_LIFETIME");
        if (!group.gameObject.activeInHierarchy || !popup.gameObject.activeInHierarchy) return No("OWNER_GROUP_INACTIVE");
        if (Ptr(Parent<Gc.FilterPopup>(group))!=Ptr(popup) || !text.transform.IsChildOf(group.transform)) return No("DIRECT_GROUP_PARENT_MISMATCH");
        long setting; string source; TextMeshProUGUI? direct;
        if (ticket.Identity=="UI6-FILTER-CheckBox" && group is Gc.CheckBoxFilterGroup check && Exact(check,"Gc.CheckBoxFilterGroup"))
        { var now=check._setting; setting=Ptr(now); source=now?._Title_k__BackingField??""; direct=check._nameText; }
        else if (ticket.Identity=="UI6-FILTER-Toggle" && group is Gc.ToggleFilterGroup toggle && Exact(toggle,"Gc.ToggleFilterGroup"))
        { var now=toggle._setting; setting=Ptr(now); source=now?._Title_k__BackingField??""; direct=toggle._nameText; }
        else return No("EXACT_CURRENT_GROUP_CLASS_MISMATCH");
        if (setting==0 || setting!=ticket.Setting || source!=ticket.Source || !Alive(direct) || Ptr(direct)!=Ptr(text)) return No("CURRENT_SETTING_SOURCE_DIRECT_TARGET_MISMATCH");
        var dictionary=popup._filterGroupDict;
        if (dictionary==null || dictionary.Count>32) return No("CURRENT_DICTIONARY_UNAVAILABLE");
        int membership=0;
        for(int i=0;i<22;i++) if(dictionary.TryGetValue((Gc.FilterType)i,out var item)&&Ptr(item)==Ptr(group)){membership++;domain=i;}
        if(membership!=1)return No($"CURRENT_DICTIONARY_MEMBERSHIP_{membership}_N{dictionary.Count}");
        ReportUi6Source(ticket.Identity,popup,"CURRENT_TYPED_SOURCE_MODEL_TARGET_READY"); return true;
    }
    void CaptureUi6Filter(Component group, TextMeshProUGUI title, long setting, string source, NativeHookContext callback)
    {
        gate.Require();
        string identity=group is Gc.CheckBoxFilterGroup&&Exact(group,"Gc.CheckBoxFilterGroup")?"UI6-FILTER-CheckBox":group is Gc.ToggleFilterGroup&&Exact(group,"Gc.ToggleFilterGroup")?"UI6-FILTER-Toggle":"";
        if(identity.Length==0||!Alive(title)||setting==0)return;
        var popup=Parent<Gc.FilterPopup>(group);
        if(!Alive(popup)){ReportUi6Source(identity,null,"CURRENT_TYPED_POPUP_PARENT_UNAVAILABLE");return;}
        var ticket=new Ui6FilterTicket(popup!.GetInstanceID(),group.GetInstanceID(),title.GetInstanceID(),setting,source,identity);
        bool Current()=>Ui6FilterCurrent(group,popup!,title,ticket);
        // Capture precedes popup dictionary finalization. Each later pass reads the group's current Setting anew.
        ReportUi6Source(identity,popup,"NATURAL_CALLBACK_CAPTURED_BEFORE_MEMBERSHIP");
        ApplyUi6Consumer(popup!,title,identity,source,Current,callback);
        CaptureCurrentFilterConsumers(group,popup!,title,ticket,callback);
    }
    void CaptureCurrentFilterConsumers(Component group, Gc.FilterPopup popup, TextMeshProUGUI title,
        Ui6FilterTicket ticket, NativeHookContext callback)
    {
        if (!callback.Active || callback.IsPrefix || callback.Lifetime <= 0 || !TryUi6TextTopology(title, out string topology)) return;
        string callbackType = ticket.Identity == "UI6-FILTER-CheckBox" ? "Gc.CheckBoxFilterGroup" : "Gc.ToggleFilterGroup";
        if (callback.Method.DeclaringType?.FullName != callbackType || callback.Method.Name != "Setup") return;
        if (!host.Snapshot.Bindings.Values.Any(b => b.Enabled && b.AdapterId == "common.model-label.v1"
            && b.SourceIdentity.Kind == "current-model"
            && (b.Selector.ScopeKey == "current-filter" && b.Selector.SlotKey == "filter_heading" && b.SourceIdentity.ExactJp == ticket.Source
                || b.Selector.ScopeKey == "current-filter" && b.Selector.SlotKey == "filter_option"
                || b.Selector.ScopeKey == "element-filter" && b.Selector.SlotKey == "element_filter_option")
            && b.Selector.Type == "model" && b.Selector.Lifetime == "owner")) return;
        Queue(group);
        if (!owners.TryGetValue(Ptr(group), out var generation)) return;
        bool Current() => Ui6FilterCurrent(group, popup, title, ticket) && TryUi6TextTopology(title, out var now) && now == topology;
        void Prepared(Owner owner) => ApplyCurrentFilterConsumers(owner, group, popup, title, ticket, Current);
        var stamp = new Ui6ProducerStamp(callbackType, "Setup", callback.Lifetime);
        var key = (Ptr(group), Ptr(title), "CURRENT-FILTER-CONSUMERS");
        if (ui6Pending.Count < 64 || ui6Pending.ContainsKey(key))
            ui6Pending[key] = new(group, title, "CURRENT-FILTER-CONSUMERS", ticket.Source, Current, stamp,
                group.GetInstanceID(), title.GetInstanceID(), generation.Lifetime, frame, null, Prepared);
        // The natural callback only creates the pure/current-owner ticket. No callback or Setting wrapper enters the retry.
        ApplyCurrentFilterConsumers(generation, group, popup, title, ticket, Current);
    }
    void ApplyCurrentFilterConsumers(Owner generation, Component group, Gc.FilterPopup popup, TextMeshProUGUI title,
        Ui6FilterTicket ticket, Func<bool> sourceCurrent)
    {
        gate.Require();
        if (!Current(generation) || Ptr(generation.Value) != Ptr(group) || !sourceCurrent()
            || !Ui6FilterCurrent(group, popup, title, ticket, out int domain)) return;
        long dictionary = Ptr(popup._filterGroupDict);
        bool CurrentDomain() => sourceCurrent() && Ui6FilterCurrent(group, popup, title, ticket, out int now)
            && now == domain && Ptr(popup._filterGroupDict) == dictionary;
        string ReadInput() { Require(CurrentDomain(), "CURRENT_FILTER_HEADING_SOURCE_CHANGED"); return ReadCurrentFilterHeadingInput(group, ticket); }
        // Same existing Consumers owner/domain contract. Sort temporary models remain callback-only.
        foreach (var binding in host.Snapshot.Bindings.Values.Where(b => b.Enabled && b.AdapterId == "common.model-label.v1"
            && b.SourceIdentity.Kind == "current-model" && b.SourceIdentity.ExactJp == ticket.Source
            && b.Selector.ScopeKey == "current-filter" && b.Selector.SlotKey == "filter_heading"
            && b.Selector.Type == "model" && b.Selector.Lifetime == "owner" && b.Selector.Domain == domain
            && b.Selector.SourceValue == null && b.Selector.SourceId == null
            && (b.Selector.Segments.IsEmpty || UnityNative.Nodes(title.transform, group.transform).SequenceEqual(b.Selector.Segments))))
            Apply(generation, title, binding, CurrentDomain, null, domain: domain, model: true,
                readInput: ReadInput, rebound: () => !CurrentDomain());
        ApplyCurrentFilterOptions(generation, group, ticket, domain, CurrentDomain);
    }
    sealed record CurrentFilterValues(long Entries, long ToggleList,
        System.Collections.Generic.KeyValuePair<int,string>[] Rows, InertToggle[] Toggles);
    CurrentFilterValues ReadCurrentFilterValues(Component group, Ui6FilterTicket ticket)
    {
        gate.Require();
        System.Collections.Generic.KeyValuePair<int,string>[] rows;
        Il2CppSystem.Collections.Generic.List<InertToggle>? toggles;
        long entries;
        if (ticket.Identity == "UI6-FILTER-CheckBox" && group is Gc.CheckBoxFilterGroup check)
        {
            var current = check._setting; Require(Ptr(current) == ticket.Setting && current != null, "CURRENT_FILTER_CHECKBOX_SETTING_CHANGED");
            var values = current!._NameAndValues_k__BackingField; Require(values != null, "CURRENT_FILTER_CHECKBOX_VALUES_UNAVAILABLE");
            entries = Ptr(values); rows = values!.Take(65).Select(x => new System.Collections.Generic.KeyValuePair<int,string>(x._Value_k__BackingField, x._Name_k__BackingField)).ToArray();
            toggles = check._toggleList;
        }
        else if (ticket.Identity == "UI6-FILTER-Toggle" && group is Gc.ToggleFilterGroup toggle)
        {
            var current = toggle._setting; Require(Ptr(current) == ticket.Setting && current != null, "CURRENT_FILTER_TOGGLE_SETTING_CHANGED");
            var values = current!._NameAndValues_k__BackingField; Require(values != null, "CURRENT_FILTER_TOGGLE_VALUES_UNAVAILABLE");
            entries = Ptr(values); rows = values!.Take(65).Select(x => new System.Collections.Generic.KeyValuePair<int,string>(x._Value_k__BackingField, x._Name_k__BackingField)).ToArray();
            toggles = toggle._toggleList;
        }
        else throw new InvalidDataException("CURRENT_FILTER_HEADING_GROUP_CLASS_CHANGED");
        Require(rows.Length <= 64 && toggles != null && toggles.Count <= 64, "CURRENT_FILTER_HEADING_INPUT_BOUND");
        return new(entries, Ptr(toggles), rows, toggles!.ToArray());
    }
    string ReadCurrentFilterHeadingInput(Component group, Ui6FilterTicket ticket)
    {
        var current = ReadCurrentFilterValues(group, ticket);
        return System.Text.Json.JsonSerializer.Serialize(new { ticket.Source, entries = current.Entries,
            list = current.ToggleList, values = current.Rows,
            toggles = current.Toggles.Select(t => new { pointer = Ptr(t), value = t._Value_k__BackingField, target = Ptr(t.text) }).ToArray() });
    }
    sealed record FilterOptionTicket(int Domain, long Entries, long ToggleList, int RowIndex, int ToggleIndex,
        int Value, string Source, int ToggleInstance, int TargetInstance, string Topology);
    bool CurrentFilterOption(Component group, TextMeshProUGUI text, InertToggle toggle,
        Ui6FilterTicket groupTicket, FilterOptionTicket option, Func<bool> currentDomain)
    {
        if (!gate.CanAccess || !currentDomain() || !Alive(toggle) || !Alive(text)
            || toggle.GetInstanceID() != option.ToggleInstance || text.GetInstanceID() != option.TargetInstance
            || !toggle.transform.IsChildOf(group.transform) || !text.transform.IsChildOf(toggle.transform)
            || Ptr(toggle.text) != Ptr(text) || toggle._Value_k__BackingField != option.Value
            || !TryUi6TextTopology(text, out var topology) || topology != option.Topology) return false;
        var current = ReadCurrentFilterValues(group, groupTicket);
        return current.Entries == option.Entries && current.ToggleList == option.ToggleList
            && option.RowIndex >= 0 && option.RowIndex < current.Rows.Length
            && option.ToggleIndex >= 0 && option.ToggleIndex < current.Toggles.Length
            && current.Rows[option.RowIndex].Key == option.Value && current.Rows[option.RowIndex].Value == option.Source
            && current.Rows.Count(x => x.Key == option.Value) == 1
            && Ptr(current.Toggles[option.ToggleIndex]) == Ptr(toggle)
            && current.Toggles.Count(t => Alive(t) && Ptr(t) == Ptr(toggle)) == 1
            && current.Toggles.Count(t => Alive(t) && t._Value_k__BackingField == option.Value) == 1;
    }
    void ApplyCurrentFilterOptions(Owner generation, Component group, Ui6FilterTicket ticket, int domain, Func<bool> currentDomain)
    {
        gate.Require();
        if (!Current(generation) || !currentDomain()) return;
        var current = ReadCurrentFilterValues(group, ticket);
        foreach (var binding in host.Snapshot.Bindings.Values.Where(b => b.Enabled && b.AdapterId == "common.model-label.v1"
            && b.SourceIdentity.Kind == "current-model" && b.Selector.Type == "model" && b.Selector.Lifetime == "owner"
            && b.Selector.Domain == domain && b.Selector.SourceValue.HasValue && b.Selector.SourceId == null
            && (b.Selector.ScopeKey == "current-filter" && b.Selector.SlotKey == "filter_option"
                || b.Selector.ScopeKey == "element-filter" && b.Selector.SlotKey == "element_filter_option")))
        {
            int value = binding.Selector.SourceValue!.Value;
            var rows = current.Rows.Select((row,index) => (row,index)).Where(x => x.row.Key == value).ToArray();
            var toggles = current.Toggles.Select((toggle,index) => (toggle,index)).Where(x => Alive(x.toggle) && x.toggle._Value_k__BackingField == value).ToArray();
            if (rows.Length != 1 || toggles.Length != 1) { BindingOutcome(generation, binding, "CURRENT_FILTER_OPTION_UNIQUE_VALUE_REQUIRED"); continue; }
            if (rows[0].row.Value != binding.SourceIdentity.ExactJp) { BindingOutcome(generation, binding, "CURRENT_FILTER_OPTION_SOURCE_CHANGED"); continue; }
            var toggle = toggles[0].toggle; var text = toggle.text;
            if (!Alive(text) || !toggle.transform.IsChildOf(group.transform) || !text!.transform.IsChildOf(toggle.transform)
                || !TryUi6TextTopology(text, out var topology)
                || !binding.Selector.Segments.IsEmpty && !UnityNative.Nodes(text.transform, group.transform).SequenceEqual(binding.Selector.Segments)) continue;
            var option = new FilterOptionTicket(domain, current.Entries, current.ToggleList, rows[0].index, toggles[0].index,
                value, rows[0].row.Value, toggle.GetInstanceID(), text.GetInstanceID(), topology);
            bool CurrentOption() => CurrentFilterOption(group, text, toggle, ticket, option, currentDomain);
            string ReadInput() { Require(CurrentOption(), "CURRENT_FILTER_OPTION_SOURCE_CHANGED"); return ReadCurrentFilterHeadingInput(group, ticket); }
            // Only live Unity owner/toggle/target and pure indices/source are retained. Setting/value wrappers stay local to each fresh read.
            Apply(generation, text, binding, CurrentOption, null, domain: domain, value: value, model: true,
                readInput: ReadInput, rebound: () => !CurrentOption());
        }
    }
    void ApplyUi6Sort(Component owner, TextMeshProUGUI text, int value, string source, Func<bool> current, NativeHookContext callback)
    {
        // Same current sorter/work/value join as the existing nine stat labels; no later sorter access.
        string identity = value == 0 ? "UI6-SORT-UI4-M015" : value == 10 ? "UI6-SORT-UI4-M019" : "";
        if (identity.Length != 0) ApplyUi6Consumer(owner, text, identity, source, current, callback, value);
    }

    sealed record Ui6ShopTicket(int OwnerInstance, int CellInstance, int TargetInstance,
        long InputModel, long SourceModel, bool Frame, string Source);
    bool Ui6ShopCurrent(Gc.Shop.ShopTopView owner, Gc.Shop.ShopCategoryScrollCell cell, TextMeshProUGUI text, Ui6ShopTicket ticket)
    {
        if(!gate.CanAccess)return false;
        string role=ticket.Frame?"UI6-UI4-M006-OWNED_CATEGORY_FRAME_TEXT":"UI6-UI4-M006-OWNED_CATEGORY_SECTION_TITLE";
        bool No(string reason){ReportUi6Source(role,cell,reason);return false;}
        if (!gate.CanAccess || !Alive(owner) || !Alive(cell) || !Alive(text) || owner.GetInstanceID() != ticket.OwnerInstance
            || cell.GetInstanceID() != ticket.CellInstance || text.GetInstanceID() != ticket.TargetInstance
            || !cell.gameObject.activeInHierarchy || !text.transform.IsChildOf(cell.transform)
            || Ptr(Parent<Gc.Shop.ShopTopView>(cell)) != Ptr(owner)) return No("CURRENT_PARENT_OR_LIFETIME");
        var controller = Parent<Gc.Shop.ShopTopController>(owner);
        if (!Alive(controller) || Ptr(controller!._view) != Ptr(owner) || Ptr(controller._viewData) != Ptr(owner._viewData)) return No("CURRENT_CONTROLLER_VIEW_DATA");
        var scene = controller.Controller; var created = scene?.createdScene; int subscene = scene?.currentSubSceneIndex ?? -1;
        if (!Alive(scene) || created == null || created.Length > 32 || subscene < 0 || subscene >= created.Length || Ptr(created[subscene]) != Ptr(controller)) return No("CURRENT_SUBSCENE");
        var view = owner._currencyShopView; var scroll = view?._scrollView; var data = scroll?._data; var scroller = scroll?._scroller;
        if (!Alive(view) || !Alive(scroll) || !Alive(scroller) || data == null || cell.dataIndex < 0 || cell.dataIndex >= data.Count) return No("CURRENT_SCROLL_OR_INDEX");
        var active = scroller!._activeCellViews; var cells = active?.data; int count = active?.Count ?? -1;
        if (cells == null || count < 0 || count > 32 || count > cells.Length || !cell.active) return No("CURRENT_ACTIVE_CELL_LIST");
        int membership = 0;
        for (int i = 0; i < count; i++) if (Alive(cells[i]) && Ptr(cells[i]) == Ptr(cell)) membership++;
        if (membership != 1) return No("CURRENT_MEMBERSHIP_N"+membership);
        var current = data[cell.dataIndex];
        if (current == null || Ptr(current) != ticket.InputModel ||
            !Exact(current, "Gc.Shop.ShopCategoryScrollCellData")) return No("CURRENT_INDEXED_MODEL_MISMATCH");
        if (ticket.Frame)
        {
            var frame = cell._frame; var model = current._FrameData_k__BackingField;
            if (Alive(cell._sectionFrame) && Ptr(cell._sectionFrame!._titleText) == Ptr(text)) return No("FRAME_IS_SECTION_TARGET");
            bool valid=Alive(frame) && Exact(frame, "Gc.Shop.ShopCategoryFrame") && model != null && Exact(model, "Gc.Shop.ShopCategoryFrameData")
                && Ptr(model) == ticket.SourceModel && Ptr(frame!._data) == ticket.SourceModel && Ptr(frame._cellText) == Ptr(text)
                && model._Text_k__BackingField == ticket.Source;
            if(!valid)return No("CURRENT_FRAME_MODEL_TARGET_OR_SOURCE");
            ReportUi6Source(role,cell,"CURRENT_FRAME_MODEL_TARGET_READY");return true;
        }
        var section = cell._sectionFrame;
        if (Alive(cell._frame) && Ptr(cell._frame!._cellText) == Ptr(text)) return No("SECTION_IS_FRAME_TARGET");
        bool sectionValid=ticket.SourceModel == ticket.InputModel && Alive(section) && Exact(section, "Gc.Shop.ShopCategorySectionFrame")
            && Ptr(section!._titleText) == Ptr(text) && current._SectionTitle_k__BackingField == ticket.Source;
        if(!sectionValid)return No("CURRENT_SECTION_MODEL_TARGET_OR_SOURCE");
        ReportUi6Source(role,cell,"CURRENT_SECTION_MODEL_TARGET_READY");return true;
    }
    void CaptureUi6Shop(Gc.Shop.ShopTopView owner, Gc.Shop.ShopCategoryScrollCell cell, TextMeshProUGUI? text,
        string identity, string source, long inputModel, long sourceModel, bool frame, NativeHookContext callback)
    {
        if (!Alive(text)) return;
        // Separate method prevents a retained guard from closing over the Setup parameter/NativeHookContext.
        // EnhancedScroller assigns the current index/membership around Setup; do not pin
        // a callback-time index. Each retry joins the current indexed model and direct frame/section model.
        var ticket = new Ui6ShopTicket(owner.GetInstanceID(), cell.GetInstanceID(), text!.GetInstanceID(),
            inputModel, sourceModel, frame, source);
        bool Current() => Ui6ShopCurrent(owner, cell, text!, ticket);
        if (ObserveAlreadyAcceptedUi6Shop(cell, text!, identity, source, Current, callback)) return;
        ApplyUi6Consumer(cell, text!, identity, source, Current, callback);
    }
    bool ObserveAlreadyAcceptedUi6Shop(Gc.Shop.ShopCategoryScrollCell cell, TextMeshProUGUI text,
        string identity, string source, Func<bool> current, NativeHookContext callback)
    {
        gate.Require();
        if (!callback.Active || callback.IsPrefix || callback.Lifetime <= 0) return false;
        string type = callback.Method.DeclaringType?.FullName ?? "";
        bool observed = false;
        foreach (var binding in host.Snapshot.Bindings.Values.Where(b => Ui6ConsumerRoutePolicy.Matches(b, identity, type, callback.Method.Name)))
        {
            var resource = host.Snapshot.Resources[binding.ResourceId];
            if (resource.Kind != "literal" || resource.Disposition != "translate" || string.IsNullOrEmpty(resource.Ko) || text.text != resource.Ko || (source != resource.Ko && source != binding.SourceIdentity.ExactJp)) continue;
            Queue(cell);
            if (!owners.TryGetValue(Ptr(cell), out var generation)) continue;
            // Observation only: the actual current model already supplies the accepted KO to its owned target.
            // This does not rewrite JP, authenticate upstream lineage, check glyphs, or claim an owned UI6 write.
            string acceptedKo = resource.Ko;
            string outcome = source == acceptedKo ? "MODEL_AND_TARGET_ALREADY_ACCEPTED_KO" : "MODEL_JP_TARGET_ALREADY_ACCEPTED_KO";
            if (current()) BindingOutcome(generation, binding, outcome);
            else if (ui6Pending.Count < 64) {
                // Setup may precede active-scroller membership. Keep only the live cell/target,
                // immutable source/ticket and original natural producer stamp, never its argument.
                var producer = new Ui6ProducerStamp(type, callback.Method.Name, callback.Lifetime);
                void Observe(Owner now) { if (current() && text.text == acceptedKo) BindingOutcome(now, binding, outcome); }
                ui6Pending[(Ptr(cell),Ptr(text),identity)] = new(cell,text,identity,source,current,producer,
                    cell.GetInstanceID(),text.GetInstanceID(),generation.Lifetime,frame,null,Observe);
            }
            observed = true;
        }
        return observed;
    }
    void Ui6ShopPost(NativeHookContext callback)
    {
        gate.Require();
        if (!callback.Active || callback.IsPrefix || callback.Instance is not Gc.Shop.ShopCategoryScrollCell cell
            || callback.Args.Length != 1 || callback.Args[0] is not Gc.Shop.ShopCategoryScrollCellData input
            || !Alive(cell) || !Exact(cell, "Gc.Shop.ShopCategoryScrollCell") || !Exact(input, "Gc.Shop.ShopCategoryScrollCellData")) return;
        var owner = Parent<Gc.Shop.ShopTopView>(cell);
        if (!Alive(owner)) return;
        Queue(cell);
        var frameData = input._FrameData_k__BackingField; var frame = cell._frame;
        ReportUi6Source("SHOP_CATEGORY_PHYSICAL_ROLE", cell,
            $"SETUP_F{(frameData != null ? 1 : 0)}_A{(Alive(frame) && frame.gameObject.activeInHierarchy ? 1 : 0)}_S{(Alive(cell._sectionFrame) && cell._sectionFrame.gameObject.activeInHierarchy ? 1 : 0)}");
        if (frameData != null && Alive(frame) && Exact(frameData, "Gc.Shop.ShopCategoryFrameData") && Exact(frame, "Gc.Shop.ShopCategoryFrame"))
            CaptureUi6Shop(owner!, cell, frame!._cellText, "UI6-UI4-M006-OWNED_CATEGORY_FRAME_TEXT", frameData._Text_k__BackingField ?? "", Ptr(input), Ptr(frameData), true, callback);
        var section = cell._sectionFrame;
        if (Alive(section) && Exact(section, "Gc.Shop.ShopCategorySectionFrame"))
            CaptureUi6Shop(owner!, cell, section!._titleText, "UI6-UI4-M006-OWNED_CATEGORY_SECTION_TITLE", input._SectionTitle_k__BackingField ?? "", Ptr(input), Ptr(input), false, callback);
    }
}
