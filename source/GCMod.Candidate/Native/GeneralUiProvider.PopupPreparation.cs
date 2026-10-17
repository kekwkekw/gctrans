#nullable enable
using System.Security.Cryptography;
using System.Text;
using GCMod.Nextgen;
using TMPro;
using UnityEngine;

namespace GCMod.Candidate.Native;

public sealed partial class GeneralUiProvider
{
    // OnLoad returns an IEnumerator. Its hook proves entry, not completion of the
    // native title/control construction. Retain only identity values across retries.
    sealed record PopupCaptionIdentity(long Owner, int OwnerInstance, long Model, long View,
        long Close, long Exchange, long Reset, int TargetInstance);
    readonly Dictionary<UiGeometryHoldKey, PopupCaptionReadiness> popupReadiness = new();
    readonly HashSet<string> popupReadinessDiagnostics = new(StringComparer.Ordinal);

    bool CapturePopupCaption(Owner owner, UiBinding b, TextMeshProUGUI text, out PopupCaptionIdentity? identity)
    {
        gate.Require(); identity = null;
        if (!Current(owner) || !Alive(text) || !PopupCaptionPreparationPolicy.Supports(b)) return false;
        var leaves = ExactPaths(owner.Value.transform, b.Selector.Segments);
        if (leaves.Count != 1 || Ptr(leaves[0]) != Ptr(text.transform) ||
            leaves[0].GetComponents<TextMeshProUGUI>().Length != 1) return false;
        long model, view = 0, close, exchange = 0, reset = 0;
        if (owner.Value is Gc.ActiveSkillDetailPopup skill && Exact(skill, "Gc.ActiveSkillDetailPopup"))
        {
            model = Ptr(skill._skill); var detail = skill._skillDetail; var button = skill._closeButton;
            if (model == 0 || !Alive(detail) || !Alive(button) || !detail.transform.IsChildOf(skill.transform)) return false;
            view = Ptr(detail); close = Ptr(button);
            if (b.Selector.SlotKey == "skill_popup_title")
            { if (!Alive(skill._titleText) || Ptr(skill._titleText) != Ptr(text)) return false; }
            else if (b.Selector.SlotKey == "skill_close")
            { if (!text.transform.IsChildOf(button.transform)) return false; }
            else if (!text.transform.IsChildOf(detail.transform)) return false;
        }
        else if (owner.Value is Gc.ExchangeLayerPiecePopup popup && Exact(popup, "Gc.ExchangeLayerPiecePopup"))
        {
            model = Ptr(popup._data); var cb = popup._closeButton; var eb = popup._exchangeButton; var rb = popup._resetMaterialButton;
            if (model == 0 || !Alive(cb) || !Alive(eb) || !Alive(rb)) return false;
            close = Ptr(cb); exchange = Ptr(eb); reset = Ptr(rb);
            var control = b.Selector.SlotKey switch { "exchange_close" => cb, "exchange_convert_button" => eb,
                "exchange_material_deselect" => rb, _ => null };
            if (control != null && !text.transform.IsChildOf(control.transform)) return false;
        }
        else return false;
        identity = new(Ptr(owner.Value), owner.Instance, model, view, close, exchange, reset, text.GetInstanceID());
        return true;
    }

    bool PreparePopupCaption(Owner owner, UiBinding b, TextMeshProUGUI text, out PopupCaptionIdentity? identity)
    {
        gate.Require(); identity = null;
        if (!PopupCaptionPreparationPolicy.Supports(b)) return true;
        if (!CapturePopupCaption(owner, b, text, out identity)) { PopupPreparationOutcome(owner,b,"IDENTITY_HELD"); return false; }
        var popup = (Gc.BasePopup)owner.Value;
        var key = new UiGeometryHoldKey(b.Id, Ptr(text), owner.Lifetime, host.Snapshot.Generations.Ui);
        if (!popupReadiness.TryGetValue(key, out var state))
        { if (popupReadiness.Count >= 256) return false; popupReadiness[key] = state = new(); }
        bool mesh = text.text == b.SourceIdentity.ExactJp && RenderedGeometryText(text) == MeshSource(text, text.text) &&
            !text.m_havePropertiesChanged && !text.m_isLayoutDirty;
        string input = string.Join("|", identity, Controls(owner.Value), ControlTopology(owner.Value),
            GeometryMotion(text), System.Text.Json.JsonSerializer.Serialize(NumericRect(text.rectTransform)));
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
        bool ready = state.Observe(gate.CanAccess, true, popup.gameObject.activeInHierarchy, popup.IsPlayOutAnim, mesh, frame, fingerprint);
        PopupPreparationOutcome(owner, b, ready ? "READY" : !popup.gameObject.activeInHierarchy || popup.IsPlayOutAnim || !mesh ? "WAIT_NATIVE" : "WAIT_STABLE");
        return ready;
    }

    Func<bool> WithPopupCaptionSourceGuard(Owner owner, GeometryTicket? ticket, Func<bool> current) =>
        ticket?.PopupCaption is not { } captured ? current : () => current() &&
            CapturePopupCaption(owner, ticket.Binding, ticket.Text, out var now) && now == captured;

    void PopupPreparationOutcome(Owner owner, UiBinding b, string phase)
    {
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(b.Id)))[..12];
        string code = "POPUP_PREPARE_" + key + "_OWNER_" + owner.Lifetime + "_" + phase;
        if (popupReadinessDiagnostics.Count < 256 && popupReadinessDiagnostics.Add(code)) log.Record(code);
    }
    void ForgetPopupPreparation(long generation)
    {
        foreach (var key in popupReadiness.Keys.Where(k => k.OwnerGeneration == generation).ToArray()) popupReadiness.Remove(key);
        popupReadinessDiagnostics.RemoveWhere(s => s.Contains("_OWNER_" + generation + "_", StringComparison.Ordinal));
    }
}
