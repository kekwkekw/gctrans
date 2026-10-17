#nullable enable
using GCMod.Nextgen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GCMod.Candidate.Native;
public sealed partial class GeneralUiProvider
{
    sealed record FilterResetGeometryIdentity(long Owner, int OwnerInstance, long Button, int ButtonInstance, int ContainerInstance, FilterPreferredCaptionRole Role);
    readonly HashSet<string> filterResetGeometryReports = new(StringComparer.Ordinal);
    bool TryFilterResetIdentity(Component value, UiBinding binding, TextMeshProUGUI text, out FilterResetGeometryIdentity? identity, out uint proof)
    {
        gate.Require(); identity = null; proof = 0;
        if (!FilterResetGeometryPolicy.Supports(binding) || value is not Gc.FilterPopup popup || !Alive(popup)
            || !Exact(popup, "Gc.FilterPopup") || !Alive(text) || !text.transform.IsChildOf(popup.transform)) return false;
        proof |= 1;
        var role = FilterResetGeometryPolicy.Role(binding);
        // Actual current interop declares all three independent InertButton fields.
        // An exact SelectAll path never borrows Reset/Deselect's identity.
        var button = role switch { FilterPreferredCaptionRole.Reset => popup._defaultAllButton,
            FilterPreferredCaptionRole.Deselect => popup._deselectAllButton,
            FilterPreferredCaptionRole.SelectAll => popup._selectAllButton, _ => null };
        var controls = new[] { popup._defaultAllButton, popup._deselectAllButton, popup._selectAllButton };
        if (Alive(button) && controls.Count(b => Alive(b) && Ptr(b) == Ptr(button)) != 1) return false;
        if (role == FilterPreferredCaptionRole.SelectAll &&
            (!Alive(button) || popup._forceHideSelectAllButton || !button!.gameObject.activeInHierarchy ||
             ExactPaths(popup.transform,binding.Selector.Segments).Count != 1 ||
             Ptr(ExactPaths(popup.transform,binding.Selector.Segments)[0]) != Ptr(text.transform))) return false;
        if (Alive(button)) proof |= 2;
        if (Alive(button) && Exact(button!, "UnityEngine.UI.InertButton")) proof |= 4;
        if (Alive(button) && text.transform.IsChildOf(button!.transform)) proof |= 8;
        if (Alive(button) && Ptr(Parent<Button>(text)) == Ptr(button)) proof |= 16;
        if (Alive(button) && Ptr(button!.textMeshProUgui) == Ptr(text)) proof |= 32;
        var container = Alive(button) ? button!.GetComponent<RectTransform>() : null;
        if (Alive(container)) proof |= 64;
        if (text.GetComponents<TextMeshProUGUI>().Length == 1) proof |= 128;
        if (proof != 255) return false;
        identity = new(Ptr(popup), popup.GetInstanceID(), Ptr(button), button!.GetInstanceID(), container!.GetInstanceID(), role);
        return true;
    }
    bool TryFilterResetGeometry(Component value, UiBinding binding, TextMeshProUGUI text,
        out FilterResetGeometryIdentity? identity, out ContentFitDriver? driver, long ownerGeneration)
    {
        gate.Require(); driver = null;
        bool direct = TryFilterResetIdentity(value, binding, text, out identity, out uint proof);
        if (direct)
        {
            driver = CaptureContentFitDriver(text);
            if (driver != null && driver.ContainerInstance == identity!.ContainerInstance) proof |= 256;
        }
        if (FilterResetGeometryPolicy.Supports(binding))
        {
            string id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
            string code = $"FRGEO_{id}_O{ownerGeneration}_U{host.Snapshot.Generations.Ui}_P{proof:X}_H{(driver?.HorizontalPreferred == true ? 1 : 0)}_V{(driver?.VerticalPreferred == true ? 1 : 0)}_R{(int)FilterResetGeometryPolicy.Role(binding)}";
            if (filterResetGeometryReports.Count < 256 && filterResetGeometryReports.Add(code))
            {
                log.Record(code);
                static int N(float n)=>float.IsFinite(n)?(int)Math.Clamp(n*100,-99999,99999):0;
                log.Record($"FRSZ_{id}_O{ownerGeneration}_T0_W{N(text.rectTransform.rect.width)}_H{N(text.rectTransform.rect.height)}");
                log.Record($"FRSP_{id}_O{ownerGeneration}_T0_W{N(text.preferredWidth)}_H{N(text.preferredHeight)}");
            }
        }
        if (proof == 511) return true;
        identity = null; driver = null; return false;
    }
    bool CurrentFilterResetIdentity(GeometryTicket ticket)
    {
        gate.Require();
        if (ticket.FilterReset == null || !ticket.OwnerCurrent() || !Alive(ticket.Text)) return false;
        var owner = Parent<Gc.FilterPopup>(ticket.Text);
        return Alive(owner) && TryFilterResetIdentity(owner!, ticket.Binding, ticket.Text, out var current, out _) && current == ticket.FilterReset;
    }
    Func<bool> WithFilterResetSourceGuard(GeometryTicket? ticket, Func<bool> current)
    {
        if (ticket is { FilterReset: not null } guarded) return () => current() && CurrentFilterResetIdentity(guarded);
        return current;
    }
    void RecordFilterPreferredCurrent(GeometryTicket t)
    {
        if(t.FilterReset?.Role!=FilterPreferredCaptionRole.SelectAll)return;
        string id=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(t.Binding.Id)))[..12];
        string key=$"FRSZ_{id}_O{t.OwnerGeneration}_T1";
        if(filterResetGeometryReports.Count>=256||!filterResetGeometryReports.Add(key))return;
        static int N(float n)=>float.IsFinite(n)?(int)Math.Clamp(n*100,-99999,99999):0;
        log.Record(key+$"_W{N(t.Text.rectTransform.rect.width)}_H{N(t.Text.rectTransform.rect.height)}");
        log.Record($"FRSP_{id}_O{t.OwnerGeneration}_T1_W{N(t.Text.preferredWidth)}_H{N(t.Text.preferredHeight)}");
    }
}
