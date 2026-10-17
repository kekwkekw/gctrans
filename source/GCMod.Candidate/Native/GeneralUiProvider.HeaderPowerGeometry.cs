#nullable enable
using GCMod.Nextgen;
using TMPro;
using UnityEngine;

namespace GCMod.Candidate.Native;
public sealed partial class GeneralUiProvider
{
    sealed record HeaderPowerGeometryIdentity(long Owner, int OwnerInstance, long Number, int NumberInstance,
        long Button, int ButtonInstance, long Root, int RootInstance, long Layer, int LayerInstance);
    readonly HashSet<string> headerPowerGeometryReports = new(StringComparer.Ordinal);
    bool TryHeaderPowerGeometry(Component value, UiBinding binding, TextMeshProUGUI text, out HeaderPowerGeometryIdentity? identity,
        bool diagnose = false, long ownerGeneration = 0)
    {
        gate.Require(); identity = null;
        if (!HeaderPowerGeometryPolicy.Supports(binding) || value is not Gc.HeaderTopController header
            || !Alive(header) || !Exact(header, "Gc.HeaderTopController") || !Alive(text)
            || !text.transform.IsChildOf(header.transform) || text.GetComponents<TextMeshProUGUI>().Length != 1) return false;
        var number = header._powerText; var button = header._powerButton; var root = header._frameUI; var layer = header._topLayerObject;
        uint proof = 0;
        if (Alive(number)) proof |= 1;
        if (Alive(button)) proof |= 2;
        if (Alive(root)) proof |= 4;
        if (Alive(layer)) proof |= 8;
        if (Alive(button) && Alive(button!.GetComponent<RectTransform>())) proof |= 16;
        if (Alive(number) && Ptr(number) != Ptr(text)) proof |= 32;
        if (Alive(number) && Ptr(text.transform.parent) == Ptr(number!.transform)) proof |= 64;
        if (Alive(number) && Alive(root) && number!.transform.IsChildOf(root!.transform)) proof |= 128;
        if (Alive(button) && Alive(root) && button!.transform.IsChildOf(root!.transform)) proof |= 256;
        if (Alive(layer) && text.transform.IsChildOf(layer!.transform)) proof |= 512;
        if (Alive(root) && root!.transform.IsChildOf(header.transform)) proof |= 1024;
        if (diagnose)
        {
            string id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
            string code = $"HGEO_{id}_O{ownerGeneration}_U{host.Snapshot.Generations.Ui}_P{proof:X}";
            if (headerPowerGeometryReports.Count < 256 && headerPowerGeometryReports.Add(code)) log.Record(code);
        }
        if (proof != 2047) return false;
        identity = new(Ptr(header), header.GetInstanceID(), Ptr(number), number.GetInstanceID(), Ptr(button), button.GetInstanceID(),
            Ptr(root), root.GetInstanceID(), Ptr(layer), layer.GetInstanceID());
        return true;
    }
    bool CurrentHeaderPowerGeometry(GeometryTicket ticket)
    {
        gate.Require();
        if (ticket.HeaderPower == null || !ticket.OwnerCurrent() || !Alive(ticket.Text)) return false;
        var owner = Parent<Gc.HeaderTopController>(ticket.Text);
        return Alive(owner) && TryHeaderPowerGeometry(owner!, ticket.Binding, ticket.Text, out var current) && current == ticket.HeaderPower;
    }
    Func<bool> WithHeaderPowerSourceGuard(GeometryTicket? ticket, Func<bool> current)
    {
        if (ticket is { HeaderPower: not null } guarded) return () => current() && CurrentHeaderPowerGeometry(guarded);
        return current;
    }
}
