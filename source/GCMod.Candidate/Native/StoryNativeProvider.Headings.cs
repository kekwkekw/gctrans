#nullable enable
using System.Diagnostics;
using System.Text.Json;
using GCMod.Nextgen;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GCMod.Candidate.Native;

internal sealed partial class StoryNativeProvider
{
    sealed class HeadingPending
    {
        public Component Owner = null!;
        public HeadingObjectIdentity OwnerIdentity = null!;
        public HeadingKind Kind;
        public int Instance, DictionaryId, Frame, WrittenFrame, Settled;
        public long Generation, UiGeneration, Started;
        // No master/closure/native callback-local wrapper survives the factory boundary.
        public AnnotationDeliveredRecord? FactoryRecord;
        public FactoryHeadingEvidence? Factory;
        public PhysicalTargetIdentity? TargetIdentity;
        public string? WrittenText;
        public string Invariant = "";
        public FontDonorHandle? Donor;
        public bool Failed, RestorePending, MonitoringComplete;
        public readonly HashSet<string> Reasons = new(StringComparer.Ordinal);
        public readonly HashSet<string> FitSamples = new(StringComparer.Ordinal);
    }
    static HeadingObjectIdentity HeadingIdentity(Component value) => new(Ptr(value), value.GetInstanceID(), UnityNative.Class(value));
    void HeadingReason(HeadingPending p, string reason)
    {
        if (p.Reasons.Count < 12 && p.Reasons.Add(reason))
            log.Record($"HD_{p.Kind.ToString().ToUpperInvariant()}_G_{p.Generation}_{reason}");
    }
    HeadingPending NewHeading(Component owner, HeadingKind kind)
    {
        long ui = selected().Generations.Ui;
        var identity = HeadingIdentity(owner);
        var pending = new HeadingPending { Owner = owner, OwnerIdentity = identity, Kind = kind, Instance = owner.GetInstanceID(),
            Generation = gate.NextLifetime(), UiGeneration = ui, Frame = frame, Started = Stopwatch.GetTimestamp() };
        // A new selection/factory assignment can naturally reuse a live owner. The lease remains
        // the same physical writer; its fresh current-record closure is rebound by TryWrite.
        if (headings.TryGetValue(Ptr(owner), out var old) && old.OwnerIdentity == identity && old.Kind == kind && old.UiGeneration == ui)
        { pending.Generation = old.Generation; pending.TargetIdentity = old.TargetIdentity; pending.WrittenText = old.WrittenText; pending.WrittenFrame = frame; }
        return pending;
    }
    void AnnotationDeliveryPost(NativeHookContext context)
    {
        if (!Enabled || context.Instance is not Gc.NovelKeyWordPopup.__c__DisplayClass2_0 closure ||
            context.Args.FirstOrDefault() is not Gc.NovelKeyWordPopup popup) return;
        if (closure.Pointer == IntPtr.Zero || IL2CPP.il2cpp_object_get_class(closure.Pointer) != Il2CppClassPointerStore<Gc.NovelKeyWordPopup.__c__DisplayClass2_0>.NativeClassPtr ||
            !Alive(popup) || IL2CPP.il2cpp_object_get_class(popup.Pointer) != Il2CppClassPointerStore<Gc.NovelKeyWordPopup>.NativeClassPtr) return;
        var master = closure.keywordMaster;
        if (master == null || master.Pointer == IntPtr.Zero || IL2CPP.il2cpp_object_get_class(master.Pointer) != Il2CppClassPointerStore<Gc.Master.NovelKeyWordMaster>.NativeClassPtr) return;
        var actual = new AnnotationDeliveredRecord(Ptr(master), master.Id, master.Keyword ?? "", master.Description ?? "", master.Ruby ?? "");
        // Optional strict-click reads can fail independently of the validated factory delivery.
        try { ObserveStrictAnnotationJoin(master, popup, actual); }
        catch { records.Remove(actual.Pointer); log.Record("ANNOTATION_STRICT_JOIN_READ_UNAVAILABLE"); }
        if (headings.Count >= 16 && !headings.ContainsKey(Ptr(popup))) return;
        if (headings.TryGetValue(Ptr(popup), out var restoring) && restoring.RestorePending) { HeadingReason(restoring, "NEW_FACTORY_RESTORE_PENDING_HELD"); return; }
        var p = NewHeading(popup, HeadingKind.AnnotationFactory); p.FactoryRecord = actual;
        headings[Ptr(popup)] = p;
        HeadingReason(p, "TYPED_FACTORY_CAPTURED");
        ApplyHeading(p);
    }
    void ObserveStrictAnnotationJoin(Gc.Master.NovelKeyWordMaster master, Gc.NovelKeyWordPopup popup, AnnotationDeliveredRecord actual)
    {
        // Strict click causality is reported independently. It cannot grant or suppress the generic factory-owned heading.
        var eligible = new List<AnnotationRecordProof>();
        foreach (var receiver in clickReceivers.Values.ToArray())
            if (TryCaptureDeliveredRecord(receiver, master, out var candidate) && candidate != null) eligible.Add(candidate);
        records.TryGetValue(Ptr(master), out var prior);
        var delivery = AnnotationDeliveryProofPolicy.Select(eligible, actual, prior);
        records.Remove(Ptr(master));
        log.Record("ANNOTATION_STRICT_JOIN_" + delivery.Reason);
        if (delivery.Record is { } record && !claimed.Contains(record.Click.Generation) &&
            clicks.TryGetValue(record.Click.Receiver, out var latest) && latest.Generation == record.Click.Generation &&
            (Stopwatch.GetTimestamp() - record.Click.Started) / (double)Stopwatch.Frequency <= 3 &&
            AnnotationProofPolicy.PopupMatches(record, new(Ptr(popup), popup.GetInstanceID(), actual.Pointer, actual.Id, actual.Keyword, actual.Description, actual.Ruby)))
        { claimed.Add(record.Click.Generation); log.Record("ANNOTATION_STRICT_RECORD_POPUP_PROVEN"); }
    }
    void DictionaryPost(NativeHookContext context)
    {
        if (!Enabled || context.Instance is not Gc.Database.DatabaseTopController owner || context.Args.FirstOrDefault() is not int id ||
            !Alive(owner) || IL2CPP.il2cpp_object_get_class(owner.Pointer) != Il2CppClassPointerStore<Gc.Database.DatabaseTopController>.NativeClassPtr ||
            headings.Count >= 16 && !headings.ContainsKey(Ptr(owner))) return;
        if (headings.TryGetValue(Ptr(owner), out var restoring) && restoring.RestorePending) { HeadingReason(restoring, "NEW_SELECTION_RESTORE_PENDING_HELD"); return; }
        var p = NewHeading(owner, HeadingKind.StandaloneDictionary); p.DictionaryId = id;
        headings[Ptr(owner)] = p; ApplyHeading(p);
    }
    bool DictionarySelected(Gc.Database.DatabaseTopController owner, int id)
    {
        if (!gate.CanAccess || !Alive(owner)) return false;
        var list = owner.keywordList; if (!Alive(list) || UnityNative.Nodes(list!.transform, owner.transform).Length == 0) return false;
        var data = list._data; var active = list._scroller?._activeCellViews; var array = active?.data;
        if (data == null || data.Count > 512 || active == null || active.Count > 32 || array == null || array.Length < active.Count) return false;
        int matches = 0;
        for (int n = 0; n < active.Count; n++)
        {
            var cell = array[n]?.TryCast<Gc.Database.NovelKeyWordCell>(); if (!Alive(cell) || !cell!.gameObject.activeInHierarchy) continue;
            var model = cell.cellData; int at = cell.dataIndex;
            if (model == null || at < 0 || at >= data.Count || Ptr(data[at]) != Ptr(model) || !model.IsSelected ||
                !Alive(cell.selectedObj) || !cell.selectedObj.activeInHierarchy || model.Id != id || UnityNative.Nodes(cell.transform, list.transform).Length == 0) continue;
            var cache = Gc.Master.NovelKeyWordMaster.cache;
            if (cache?.dictionary != null && cache.dictionary.ContainsKey(id))
            { var master = cache.dictionary[id]; if (master != null && master.Id == model.Id && master.Keyword == model.KeyWord) matches++; }
        }
        return matches == 1;
    }
    Transform? PopupPath(Gc.NovelKeyWordPopup owner, int[] indices, string[] names, int[] children, bool fallbackHeader = false)
    {
        var node = owner.transform;
        for (int n = 0; n < names.Length; n++)
        {
            if (n > 0) { int index = indices[n - 1]; if (node.childCount > 64 || index < 0 || index >= node.childCount) return null; node = node.GetChild(index); }
            if (n > 0 && node.name != names[n]) return null;
            bool generated = fallbackHeader && n == names.Length - 1;
            if (!generated && node.childCount != children[n]) return null;
            if (generated)
            {
                if (node.childCount > 4) return null; var t = node.GetComponent<TextMeshProUGUI>();
                for (int k = 0; k < node.childCount; k++)
                { var s = node.GetChild(k).GetComponents<TMP_SubMeshUI>(); if (s.Length != 1 || Ptr(s[0].textComponent) != Ptr(t)) return null; }
            }
        }
        return node;
    }
    bool ReadFactoryHeading(HeadingPending pending, Gc.NovelKeyWordPopup owner, out TextMeshProUGUI? target, out FactoryHeadingObservation? observation)
    {
        target = null; observation = null;
        if (!gate.CanAccess || !Alive(owner) || HeadingIdentity(owner) != pending.OwnerIdentity ||
            selected().Generations.Ui != pending.UiGeneration || !owner.gameObject.activeInHierarchy) return false;
        var parts = owner.keywordDetailParts; var close = owner.closeButton;
        if (!Alive(parts) || !Alive(close) || !Alive(parts.titleText) || !Alive(parts.detailText) || !Alive(parts.titleRubyText))
        { HeadingReason(pending, "DIRECT_PARTS_UNAVAILABLE"); return false; }
        var p = PopupPath(owner, new[] { 0, 1, 0 }, new[] { "NovelKeyWordPopup", "Frame_Popup", "Frame_Content", "Window_Left" }, new[] { 1, 2, 1, 3 });
        var c = PopupPath(owner, new[] { 0, 0, 4, 1 }, new[] { "NovelKeyWordPopup", "Frame_Popup", "PopupBase", "GC_ButtonSet", "BtNormalM_close" }, new[] { 1, 2, 5, 4, 6 });
        var h = PopupPath(owner, new[] { 0, 0, 2, 1 }, new[] { "NovelKeyWordPopup", "Frame_Popup", "PopupBase", "TitleWindow", "Text_Title" }, new[] { 1, 2, 5, 2, 0 }, true);
        if (!Alive(p) || !Alive(c) || !Alive(h) || Ptr(p) != Ptr(parts.transform) || Ptr(c) != Ptr(close.transform))
        { HeadingReason(pending, "SERIALIZED_PATH_MISMATCH"); return false; }
        var targets = h!.GetComponents<TextMeshProUGUI>(); var localizers = h.GetComponents<Gc.Localizer.TextLocalizer>();
        if (targets.Length != 1 || localizers.Length != 1) { HeadingReason(pending, "HEADER_COMPONENT_AMBIGUOUS"); return false; }
        target = targets[0]; var localizer = localizers[0];
        long OwnerOf(Component child) => UnityNative.Nodes(child.transform, owner.transform).Length > 0 ? Ptr(owner) : 0;
        observation = new(HeadingIdentity(owner), pending.Generation, pending.UiGeneration, owner.gameObject.activeInHierarchy,
            HeadingIdentity(parts), HeadingIdentity(close), HeadingIdentity(target), HeadingIdentity(localizer), HeadingIdentity(parts.titleText), HeadingIdentity(parts.detailText), HeadingIdentity(parts.titleRubyText),
            OwnerOf(parts), OwnerOf(close), OwnerOf(target), OwnerOf(parts.titleText), OwnerOf(parts.detailText), OwnerOf(parts.titleRubyText),
            string.Join("/", UnityNative.Nodes(p!, owner.transform)), string.Join("/", UnityNative.Nodes(c!, owner.transform)), string.Join("/", UnityNative.Nodes(h, owner.transform)),
            Ptr(localizer.text), (int)localizer.type, localizer.textConfigKey ?? "", target.text ?? "", parts.titleText.text ?? "", parts.detailText.text ?? "", parts.titleRubyText.text ?? "");
        return true;
    }
    string HeadingInvariant(HeadingPending p, TextMeshProUGUI target)
    {
        object FontRect(TextMeshProUGUI t) => new { pointer = Ptr(t), font = Ptr(t.font), material = Ptr(t.fontSharedMaterial), size = t.fontSize, rect = RectState(t.rectTransform) };
        if (p.Owner is Gc.NovelKeyWordPopup popup)
        {
            var parts = popup.keywordDetailParts; var close = popup.closeButton;
            return JsonSerializer.Serialize(new { owner = Ptr(popup), parts = Ptr(parts), close = Ptr(close), click = Ptr(close.onClick), close.interactable,
                title = parts.titleText.text, body = parts.detailText.text, ruby = parts.titleRubyText.text,
                titleStyle = FontRect(parts.titleText), bodyStyle = FontRect(parts.detailText), rubyStyle = FontRect(parts.titleRubyText),
                target = FontRect(target), localizer = Ptr(target.GetComponent<Gc.Localizer.TextLocalizer>()) });
        }
        var header = GameSystem._Instance_k__BackingField?._header?._TopController_k__BackingField;
        if (!Alive(header)) throw new InvalidOperationException("CURRENT_HEADER_REQUIRED");
        return JsonSerializer.Serialize(new { header = Ptr(header), target = FontRect(target), back = Ptr(header!._backButton), action = Ptr(header._backAction),
            home = Ptr(header._homeButton), menu = Ptr(header._menuButton), backClick = Alive(header._backButton) ? Ptr(header._backButton.onClick) : 0,
            homeClick = Alive(header._homeButton) ? Ptr(header._homeButton.onClick) : 0, menuClick = Alive(header._menuButton) ? Ptr(header._menuButton.onClick) : 0 });
    }
    void ApplyHeading(HeadingPending p)
    {
        if (!Enabled || p.Failed || !Alive(p.Owner) || HeadingIdentity(p.Owner) != p.OwnerIdentity || selected().Generations.Ui != p.UiGeneration || !p.Owner.gameObject.activeInHierarchy) return;
        TextMeshProUGUI? text = null; Func<bool> verify;
        if (p.Kind == HeadingKind.AnnotationFactory && p.FactoryRecord != null && p.Owner is Gc.NovelKeyWordPopup popup)
        {
            if (!ReadFactoryHeading(p, popup, out text, out var observation) || observation == null) { if (p.WrittenText != null) FailHeading(p, "FACTORY_CURRENT_TARGET_LOST"); return; }
            string? own = p.TargetIdentity?.NativeObject == (ulong)Ptr(text) && p.TargetIdentity.InstanceId == text!.GetInstanceID() ? p.WrittenText : null;
            if (p.Factory == null)
            {
                if (!FactoryHeadingPolicy.TryCapture(p.FactoryRecord, observation, own, out var evidence, out var reason)) { HeadingReason(p, reason); return; }
                p.Factory = evidence; HeadingReason(p, "FACTORY_OWNER_PROOF_PASS");
            }
            else if (!FactoryHeadingPolicy.IsCurrent(p.Factory, observation, own, out var reason)) { if (p.WrittenText != null) FailHeading(p, reason); else HeadingReason(p, reason); return; }
            verify = () => ReadFactoryHeading(p, popup, out var now, out var observed) && observed != null && Ptr(now) == Ptr(text) &&
                FactoryHeadingPolicy.IsCurrent(p.Factory!, observed, p.WrittenText, out _);
        }
        else if (p.Kind == HeadingKind.StandaloneDictionary && p.Owner is Gc.Database.DatabaseTopController dictionary)
        {
            var controller = dictionary.Controller; var scenes = controller?.createdScene; int index = controller?.currentSubSceneIndex ?? -1;
            if (!DictionarySelected(dictionary, p.DictionaryId) || !Alive(controller) || scenes == null || scenes.Length > 32 || index < 0 || index >= scenes.Length ||
                Ptr(scenes[index]) != Ptr(dictionary) || dictionary._HeaderSubSceneTitle_k__BackingField != "辞典") { HeadingReason(p, "DICTIONARY_CURRENT_SELECTION_REQUIRED"); return; }
            var header = GameSystem._Instance_k__BackingField?._header?._TopController_k__BackingField;
            if (!Alive(header) || UnityNative.Class(header) != "Gc.HeaderTopController") return;
            text = header!._subSceneTitleText;
            if (!Alive(text) || UnityNative.Nodes(text!.transform, header.transform).Length == 0) return;
            verify = () => gate.CanAccess && Alive(dictionary) && HeadingIdentity(dictionary) == p.OwnerIdentity && selected().Generations.Ui == p.UiGeneration &&
                dictionary._HeaderSubSceneTitle_k__BackingField == "辞典" && DictionarySelected(dictionary, p.DictionaryId) && Alive(header) &&
                Ptr(GameSystem._Instance_k__BackingField?._header?._TopController_k__BackingField) == Ptr(header) && Ptr(header._subSceneTitleText) == Ptr(text) &&
                UnityNative.Nodes(text!.transform, header.transform).Length > 0 && Alive(controller) && controller!.currentSubSceneIndex == index &&
                controller.createdScene != null && index < controller.createdScene.Length && Ptr(controller.createdScene[index]) == Ptr(dictionary);
            HeadingReason(p, "DICTIONARY_CURRENT_OWNER_PROOF_PASS");
        }
        else return;
        if (!Alive(text) || !text!.enabled || !text.gameObject.activeInHierarchy) return;
        string adapter = p.Kind == HeadingKind.AnnotationFactory ? "story.annotation-heading.v1" : "story.dictionary-heading.v1";
        var snapshot = selected(); var rules = snapshot.Bindings.Values.Where(x => x.Enabled && x.AdapterId == adapter).ToArray(); if (rules.Length != 1) return;
        var binding = rules[0]; var resource = snapshot.Resources[binding.ResourceId];
        if (resource.Jp != "辞典" || !registry.CanExecute(binding, false) || !new UiResourceResolver(activeMaster).TryResolve(resource, out var ko)) return;
        if (!Alive(text.font) || text.font.name != "GameFont") { HeadingReason(p, "HEADING_PRIMARY_ROUTE_REQUIRED"); if (p.WrittenText != null) FailHeading(p, "HEADING_PRIMARY_ROUTE_REQUIRED"); return; }
        var donor = p.Donor ?? fonts.Current;
        var font = HeadingFontPolicy.Check(gate.CanAccess, p.Kind, ko, donor, () => fonts.Current,
            scalar => donor != null && fonts.Service.Supports(new System.Text.Rune(scalar).ToString(), donor), value => fonts.Glyphs(text.font, value));
        if (!font.Allowed) { HeadingReason(p, font.Reason); if (p.WrittenText != null) FailHeading(p, font.Reason); return; }
        var identity = new PhysicalTargetIdentity((ulong)Ptr(text), text.GetInstanceID(), UnityNative.Class(text), p.Generation);
        var target = new UnityTextTarget(gate, text, () => {
            bool live = gate.CanAccess && Alive(p.Owner) && Alive(text) && HeadingIdentity(p.Owner) == p.OwnerIdentity;
            bool ownerActive = live && p.Owner.gameObject.activeInHierarchy;
            bool bound = ownerActive && verify(); bool valid = bound && text.gameObject.activeInHierarchy;
            return new TextTargetState(identity, valid ? text.text ?? "" : "", p.Generation, p.Generation, binding.SourceIdentity.IdentityJson, resource.Jp, valid, !live || ownerActive && !bound);
        });
        if (p.WrittenText == null && text.text == "辞典") HeadingFitDetails(p, text, "JP");
        string before = HeadingInvariant(p, text);
        var written = ownership.TryWrite(target, new("Story", binding.Id, null, binding.Selector.SlotKey, snapshot.Generations.Ui, target.ReadState()), ko);
        if (written.Written || written.Reason == "OWNED_VALUE_UNCHANGED")
        {
            p.TargetIdentity = identity; p.WrittenText = ko; p.Donor = donor;
            if (written.Written) { p.WrittenFrame = frame; p.Settled = 0; p.Invariant = before; HeadingReason(p, "HEADING_TEXT_WRITTEN"); }
            if (text.text != ko || HeadingInvariant(p, text) != before) { FailHeading(p, "HEADING_ONLY_INVARIANCE_FAILED"); return; }
            ObserveHeadingMesh(p, text);
        }
        else { HeadingReason(p, written.Reason); if (written.Reason == "EXTERNAL_CHANGE_YIELDED") p.Failed = true; }
    }
    void FailHeading(HeadingPending p, string reason)
    {
        HeadingReason(p, reason); p.Failed = true;
        if (p.TargetIdentity == null) return;
        var result = ownership.RestoreTargetDetailed(p.TargetIdentity);
        p.RestorePending = result is RestoreOutcome.RetryableReadFailure or RestoreOutcome.RetryableWriteFailure;
        HeadingReason(p, "HEADING_RESTORE_" + result.ToString().ToUpperInvariant());
    }
    void HeadingFitDetails(HeadingPending p, TextMeshProUGUI text, string phase)
    {
        if (!gate.CanAccess || p.FitSamples.Count >= 3 || !p.FitSamples.Add(phase)) return;
        static int Metric(float value) => float.IsFinite(value) ? (int)Math.Clamp(value * 100, -9999999, 9999999) : 9999999;
        var info = text.textInfo; var rect = text.rectTransform.rect;
        var visible = info?.characterInfo != null && info.characterCount > 0 && info.characterCount <= 256 && info.characterCount <= info.characterInfo.Length
            ? info.characterInfo.Take(info.characterCount).Where(c => c.isVisible).ToArray() : [];
        var corners = visible.SelectMany(c => new[] { c.bottomLeft, c.topLeft, c.topRight, c.bottomRight }).ToArray();
        float lowX = corners.Length == 0 ? 0 : corners.Min(v => v.x), lowY = corners.Length == 0 ? 0 : corners.Min(v => v.y),
            highX = corners.Length == 0 ? 0 : corners.Max(v => v.x), highY = corners.Length == 0 ? 0 : corners.Max(v => v.y);
        int channels = 0; foreach (var c in visible) if (c.materialReferenceIndex is >= 0 and <= 8) channels |= 1 << c.materialReferenceIndex;
        int flags = (text.isTextTruncated ? 1 : 0) | (text.firstOverflowCharacterIndex >= 0 ? 2 : 0) |
            (corners.Any(v => v.x < rect.xMin - .05f || v.x > rect.xMax + .05f || v.y < rect.yMin - .05f || v.y > rect.yMax + .05f) ? 4 : 0) |
            (text.m_havePropertiesChanged ? 8 : 0) | (text.m_isLayoutDirty ? 16 : 0);
        string role = p.Kind == HeadingKind.AnnotationFactory ? "A" : "D";
        string at = phase == "JP" ? "JP" : phase == "KO_HEADING_SETTLED" ? "KOS" : phase == "KO_HEADING_WAIT_CHANNEL" ? "KOC" : "KOF";
        log.Record($"HDF_{role}_G{p.Generation}_{at}_F{flags}_OV{(int)text.overflowMode}_I{text.firstOverflowCharacterIndex}_N{info?.characterCount ?? 0}_V{visible.Length}_R{channels}");
        log.Record($"HDFR_{role}_G{p.Generation}_{at}_X{Metric(rect.xMin)}_Y{Metric(rect.yMin)}_W{Metric(rect.width)}_H{Metric(rect.height)}");
        log.Record($"HDFG_{role}_G{p.Generation}_{at}_X{Metric(lowX)}_Y{Metric(lowY)}_W{Metric(highX-lowX)}_H{Metric(highY-lowY)}");
    }
    void ObserveHeadingMesh(HeadingPending p, TextMeshProUGUI text)
    {
        if (!gate.CanAccess || p.WrittenText == null || p.Donor != fonts.Current) return;
        string reason = "HEADING_SETTLED";
        var info = text.textInfo;
        if (text.m_havePropertiesChanged || text.m_isLayoutDirty || info?.characterInfo == null || info.characterCount <= 0 || info.characterCount > 256 || info.characterCount > info.characterInfo.Length)
            reason = "HEADING_WAIT_MESH";
        else
        {
            var chars = info.characterInfo.Take(info.characterCount).ToArray();
            string mesh = string.Concat(chars.Select(c => char.ConvertFromUtf32(c.character)));
            if (mesh != p.WrittenText) reason = "HEADING_WAIT_MESH";
            else if (text.isTextTruncated || text.firstOverflowCharacterIndex >= 0) reason = "HEADING_OVERFLOW";
            else
            {
                foreach (var c in chars)
                {
                    if (ChoiceFontRolePolicy.RequiresMedium(c.character) && (!c.isVisible || Ptr(c.fontAsset) != Ptr(fonts.Medium))) { reason = "HEADING_RENDERED_MEDIUM_MISMATCH"; break; }
                    if (!c.isVisible) { if (!char.IsWhiteSpace((char)c.character)) { reason = "HEADING_GLYPH_HIDDEN"; break; } continue; }
                    int index = c.materialReferenceIndex; CanvasRenderer? renderer = null;
                    if (index == 0) { if (Ptr(c.fontAsset) == Ptr(text.font)) renderer = text.canvasRenderer; }
                    else { var subs = text.m_subTextObjects; if (index is > 0 and <= 8 && subs != null && index < subs.Length) { var sub = subs[index];
                        if (Alive(sub) && sub.enabled && sub.gameObject.activeInHierarchy && Ptr(sub.m_TextComponent) == Ptr(text) &&
                            Ptr(sub.fontAsset) == Ptr(c.fontAsset) && sub.transform.IsChildOf(text.transform)) renderer = sub.m_CanvasRenderer; } }
                    if (!Alive(renderer) || renderer!.cull || renderer.GetAlpha() <= 0) { reason = "HEADING_WAIT_CHANNEL"; break; }
                    var corners = new[] { c.bottomLeft, c.topLeft, c.topRight, c.bottomRight };
                    bool Inside(Vector3 v, Rect r) => v.x >= r.xMin - .05f && v.x <= r.xMax + .05f && v.y >= r.yMin - .05f && v.y <= r.yMax + .05f;
                    // The accepted generic heading preserves the native renderer's Overflow contract.
                    // Its TMP leaf is not a clipping region in this mode; actual masks, full mesh,
                    // truncation/overflow flags and rendered glyph channels remain checked separately.
                    if (text.overflowMode != TextOverflowModes.Overflow && corners.Any(v => !Inside(v, text.rectTransform.rect)))
                    { reason = "HEADING_LEAF_CLIPPED"; break; }
                    int depth = 0;
                    for (var node = text.transform; Alive(node); node = node.parent)
                    { if (++depth > 32) { reason = "HEADING_MASK_SCOPE"; break; } var mask = node.GetComponent<RectMask2D>(); if (!Alive(mask) || !mask.enabled) continue;
                        var rect = node.GetComponent<RectTransform>(); if (!Alive(rect) || corners.Any(v => !Inside(rect.InverseTransformPoint(text.transform.TransformPoint(v)), rect.rect))) { reason = "HEADING_MASK_CLIPPED"; break; } }
                    if (reason != "HEADING_SETTLED") break;
                }
            }
        }
        if (reason != "HEADING_WAIT_MESH") HeadingFitDetails(p, text, "KO_" + reason);
        if (reason == "HEADING_SETTLED") { if (++p.Settled >= 2) HeadingReason(p, reason); }
        else { p.Settled = 0; HeadingReason(p, reason); }
        // Only observe normal native mesh/layout settlement. No ForceMeshUpdate, font, cull, rect or size mutation.
        if (frame - p.WrittenFrame >= 60 && p.Settled == 0) FailHeading(p, "HEADING_SETTLEMENT_EXPIRED");
    }
    void TickHeadings()
    {
        foreach (var row in headings.ToArray())
        {
            var p = row.Value;
            if (p.RestorePending) { FailHeading(p, "HEADING_RESTORE_RETRY"); if (p.RestorePending) continue; }
            if (!Alive(p.Owner) || HeadingIdentity(p.Owner) != p.OwnerIdentity || selected().Generations.Ui != p.UiGeneration)
            { if (p.TargetIdentity != null) FailHeading(p, "HEADING_OWNER_ENDED"); if (!p.RestorePending) headings.Remove(row.Key); continue; }
            // Keep only bounded owner/lease bookkeeping after finite settlement. Natural same-owner
            // selection/reopen can refresh this lease without treating our existing KO as native JP.
            // No record/path/glyph/tree reads run on these stable frames.
            if (p.MonitoringComplete || p.Failed) continue;
            bool expired = frame - p.Frame > 120 || (Stopwatch.GetTimestamp() - p.Started) / (double)Stopwatch.Frequency > 3;
            if (expired)
            {
                if (p.WrittenText == null) { headings.Remove(row.Key); continue; }
                if (p.Settled == 0) FailHeading(p, "HEADING_UNSETTLED_EXPIRED");
                p.MonitoringComplete = !p.RestorePending; continue;
            }
            if (p.WrittenText != null || frame - p.Frame is 1 or 3 or 8 or 20 or 60)
                try { ApplyHeading(p); } catch { HeadingReason(p, "HEADING_NATIVE_READ_ISOLATED"); }
        }
    }
}
