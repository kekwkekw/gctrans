#nullable enable
using GCMod.Nextgen;
using TMPro;
using UnityEngine;

namespace GCMod.Candidate.Native;
public sealed partial class GeneralUiProvider
{
    sealed record CampaignCaptionIdentity(long Owner, int OwnerInstance, long Group, int GroupInstance);
    readonly HashSet<string> campaignCaptionReports = new(StringComparer.Ordinal);
    bool TryCampaignCaptionIdentity(Component value, UiBinding binding, TextMeshProUGUI text, out CampaignCaptionIdentity? identity, out uint proof)
    {
        gate.Require(); identity = null; proof = 0;
        if (!CampaignCaptionPolicy.Supports(binding) || value is not Gc.HeaderTopController header || !Alive(header)
            || !Exact(header, "Gc.HeaderTopController") || !Alive(text) || !text.transform.IsChildOf(header.transform)) return false;
        var actual = header._campaignText; var group = header._campaignCanvasGroup;
        if (Alive(actual)) proof |= 1;
        if (Ptr(actual) == Ptr(text)) proof |= 2;
        if (Alive(group)) proof |= 4;
        if (Alive(group) && text.transform.IsChildOf(group!.transform)) proof |= 8;
        if (text.GetComponents<TextMeshProUGUI>().Length == 1) proof |= 16;
        if (Alive(group) && group!.transform.IsChildOf(header.transform)) proof |= 32;
        if (proof != 63) return false;
        identity = new(Ptr(header), header.GetInstanceID(), Ptr(group), group!.GetInstanceID());
        return true;
    }
    bool PrepareCampaignCaption(Owner owner, UiBinding binding, TextMeshProUGUI text, out CampaignCaptionIdentity? identity)
    {
        gate.Require();
        bool typed = TryCampaignCaptionIdentity(owner.Value, binding, text, out identity, out uint proof);
        if (!CampaignCaptionPolicy.Supports(binding)) return true;
        bool active = Alive(text) && text.enabled && text.gameObject.activeInHierarchy;
        var canvas = Alive(text) ? text.canvas : null; var renderer = Alive(text) ? text.canvasRenderer : null;
        bool canvasVisible = Alive(canvas) && canvas!.enabled && canvas.gameObject.activeInHierarchy;
        float rendererAlpha = Alive(renderer) ? renderer!.GetAlpha() : 0;
        bool rendererVisible = Alive(renderer) && !renderer!.cull && float.IsFinite(rendererAlpha) && rendererAlpha > 0;
        var rect = text.rectTransform.rect;
        var actualGroup = owner.Value is Gc.HeaderTopController header ? header._campaignCanvasGroup : null;
        float groupAlpha = Alive(actualGroup) ? actualGroup!.alpha : 0;
        bool positive = float.IsFinite(rect.width) && float.IsFinite(rect.height) && rect.width > 0 && rect.height > 0;
        bool groupsVisible = true; int ancestors = 0;
        for (var current = text.transform; Alive(current); current = current.parent)
        {
            if (++ancestors > 32) { groupsVisible = false; break; }
            var group = current.GetComponent<CanvasGroup>();
            if (Alive(group) && (!float.IsFinite(group!.alpha) || group.alpha <= 0)) groupsVisible = false;
        }
        if (active) proof |= 64;
        if (canvasVisible) proof |= 128;
        if (rendererVisible) proof |= 256;
        if (positive) proof |= 512;
        if (groupsVisible) proof |= 1024;
        string id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
        string key = $"{id}_O{owner.Lifetime}_U{host.Snapshot.Generations.Ui}";
        if (campaignCaptionReports.Count < 256 && campaignCaptionReports.Add(key))
        {
            static int M(float n) => float.IsFinite(n) ? (int)Math.Clamp(n * 100, -99999, 99999) : 99999;
            bool visible = active && canvasVisible && rendererVisible && groupsVisible;
            string phase = !typed ? "TYPED_CURRENT_UNAVAILABLE" : !visible && !positive ? "CURRENT_HIDDEN_INVALID_RECT" : !visible ? "CURRENT_HIDDEN" : !positive ? "CURRENT_INVALID_RECT" : "CURRENT_READY";
            log.Record($"CAMP_{key}_P{proof:X}_W{M(rect.width)}_H{M(rect.height)}_A{M(rendererAlpha)}_G{M(groupAlpha)}");
            log.Record($"CAMP_{key}_{phase}");
        }
        return typed && active && canvasVisible && rendererVisible && groupsVisible && positive;
    }
    bool CurrentCampaignCaptionIdentity(GeometryTicket ticket)
    {
        gate.Require();
        if (ticket.CampaignCaption == null || !ticket.OwnerCurrent() || !Alive(ticket.Text)) return false;
        var owner = Parent<Gc.HeaderTopController>(ticket.Text);
        return Alive(owner) && TryCampaignCaptionIdentity(owner!, ticket.Binding, ticket.Text, out var current, out _) && current == ticket.CampaignCaption;
    }
    Func<bool> WithCampaignCaptionSourceGuard(GeometryTicket? ticket, Func<bool> current)
    {
        if (ticket is { CampaignCaption: not null } guarded) return () => current() && CurrentCampaignCaptionIdentity(guarded);
        return current;
    }
}
