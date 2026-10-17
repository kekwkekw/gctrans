#nullable enable
using GCMod.Nextgen;
using TMPro;

namespace GCMod.Candidate.Native;

public sealed partial class GeneralUiProvider
{
    // Exact HeaderPower role only. No visibility, geometry, font or control eligibility override.
    sealed class HeaderEntryTrace
    {
        public TextMeshProUGUI? Text;
        public int TargetInstance;
        public int QueueSignals;
        public int FirstFrame, LastQueueFrame;
        public readonly HashSet<string> Reports = new(StringComparer.Ordinal);
    }
    readonly Dictionary<long, HeaderEntryTrace> headerEntryTraces = new();
    sealed record HeaderEntryPending(TextMeshProUGUI Text, UiBinding Binding, CoreProducerIdentity Producer,
        HeaderEntryIdentity Identity, long Number, HeaderEntryReadinessState State);
    readonly Dictionary<long, HeaderEntryPending> headerEntryPending = new();
    void TrackHeaderProducer(Owner owner, TextMeshProUGUI text, UiBinding binding, CoreProducerIdentity producer)
    {
        gate.Require();
        if (!HeaderPowerGeometryPolicy.Supports(binding) || owner.Value is not Gc.HeaderTopController header || !Current(owner)) return;
        var identity = new HeaderEntryIdentity(Ptr(owner.Value), owner.Instance, owner.Lifetime, owner.Scene,
            Ptr(text), text.GetInstanceID(), host.Snapshot.Generations.Ui);
        if (headerEntryPending.TryGetValue(owner.Lifetime, out var old))
        {
            // Polling/duplicate producer observations never silently replace a captured target.
            if (old.Identity != identity || old.Producer != producer) headerEntryPending.Remove(owner.Lifetime);
            return;
        }
        if (headerEntryPending.Count >= 64 || !Alive(header._powerText) || Ptr(text.transform.parent) != Ptr(header._powerText.transform)) return;
        headerEntryPending[owner.Lifetime] = new(text, binding, producer, identity, Ptr(header._powerText), new(identity));
        TraceHeaderEntry(owner, "PENDING_VALIDATED", text, binding, true);
    }
    void ObserveQueuedHeader(Owner owner)
    {
        gate.Require();
        if (owner.Value is not Gc.HeaderTopController || !Current(owner)) return;
        // Existing typed callback, exact compiled role, finite owner-relative path. No global object scan.
        foreach (var binding in host.Snapshot.Bindings.Values.Where(HeaderPowerGeometryPolicy.Supports))
        {
            var leaves = CoreTargets(owner, binding);
            if (leaves.Count != 1) continue;
            var texts = leaves[0].GetComponents<TextMeshProUGUI>();
            if (texts.Length == 1) TryCoreProducer(owner, texts[0], binding, out _);
        }
    }
    bool TickHeaderReadiness(ref Owner owner)
    {
        gate.Require();
        if (!headerEntryPending.TryGetValue(owner.Lifetime, out var pending)) return false;
        var header = owner.Value as Gc.HeaderTopController;
        bool ownerCurrent = Current(owner) && Alive(header);
        bool targetCurrent = ownerCurrent && Alive(pending.Text) && pending.Text.GetInstanceID() == pending.Identity.TargetInstance
            && Ptr(header!._powerText) == pending.Number && Ptr(pending.Text.transform.parent) == Ptr(header._powerText.transform);
        var donor = fonts.Current;
        bool sourceCurrent = targetCurrent && CoreSourceCurrent(owner, pending.Text, pending.Binding);
        var observed = new HeaderEntryReadiness(new(Ptr(owner.Value), owner.Instance, owner.Lifetime, owner.Scene,
            targetCurrent ? Ptr(pending.Text) : 0, targetCurrent ? pending.Text.GetInstanceID() : 0, host.Snapshot.Generations.Ui),
            true, ownerCurrent, targetCurrent, ownerCurrent && owner.Value.gameObject.activeInHierarchy,
            targetCurrent && pending.Text.enabled, targetCurrent && pending.Text.gameObject.activeInHierarchy,
            sourceCurrent, donor != null, donor?.Generation ?? 0);
        var result = pending.State.Observe(observed);
        if (result == HeaderEntryReadinessResult.Invalidated)
        {
            TraceHeaderEntry(owner, "PENDING_INVALIDATED"); headerEntryPending.Remove(owner.Lifetime); return false;
        }
        if (result == HeaderEntryReadinessResult.Rearm)
        {
            // Full producer/owner/localizer/path proof is required again at the actual ready edge.
            if (!CoreProducerCurrent(owner, pending.Text, pending.Binding, pending.Producer))
            {
                TraceHeaderEntry(owner, "READY_PRODUCER_REJECTED"); headerEntryPending.Remove(owner.Lifetime); return false;
            }
            owner = owner with { Started = frame - 1 };
            owners[Ptr(owner.Value)] = owner;
            TraceHeaderEntry(owner, "READY_EDGE_REARM", pending.Text, pending.Binding, true);
        }
        return true;
    }
    void TraceHeaderEntry(Owner owner, string phase, TextMeshProUGUI? text = null, UiBinding? binding = null, bool? sourceCurrent = null)
    {
        gate.Require();
        if (owner.Value is not Gc.HeaderTopController || !Alive(owner.Value)) return;
        if (binding != null && !HeaderPowerGeometryPolicy.Supports(binding)) return;
        if (!headerEntryTraces.TryGetValue(owner.Lifetime, out var trace))
        {
            if (headerEntryTraces.Count >= 64) return;
            headerEntryTraces[owner.Lifetime] = trace = new() { FirstFrame = frame, LastQueueFrame = frame };
        }
        if (phase == "QUEUE") { trace.QueueSignals++; trace.LastQueueFrame = frame; }
        if (text != null) { trace.Text = text; trace.TargetInstance = text.GetInstanceID(); }
        var target = trace.Text;
        bool targetAlive = Alive(target) && target!.GetInstanceID() == trace.TargetInstance;
        uint mask = Current(owner) ? 1u : 0u;
        if (owner.Value.gameObject.activeInHierarchy) mask |= 2;
        if (targetAlive) mask |= 4;
        if (targetAlive && target!.enabled) mask |= 8;
        if (targetAlive && target!.gameObject.activeInHierarchy) mask |= 16;
        if (sourceCurrent == true) mask |= 32;
        if (binding != null && targetAlive && target!.text == binding.SourceIdentity.ExactJp) mask |= 64;
        if (binding != null && targetAlive && CoreSourceCurrent(owner, target!, binding) && target!.text != binding.SourceIdentity.ExactJp) mask |= 128;
        if (fonts.Current != null) mask |= 256;
        long ui = host?.Snapshot.Generations.Ui ?? 0;
        int age = frame - owner.Started;
        int bucket = age <= 1 ? 1 : age <= 3 ? 3 : age <= 8 ? 8 : age <= 20 ? 20 : age <= 60 ? 60 : age <= 90 ? 90 : age <= 180 ? 180 : 181;
        string key = $"{phase}_M{mask:X}_T{trace.TargetInstance}_U{ui}_B{bucket}";
        if (trace.Reports.Count < 128 && trace.Reports.Add(key))
            log.Record($"HENTRY_6912CE447EFD_O{owner.Lifetime}_U{ui}_{phase}_M{mask:X}_T{trace.TargetInstance}_Q{trace.QueueSignals}_F{age}_A{frame-trace.FirstFrame}_L{frame-trace.LastQueueFrame}");
    }
    void ForgetHeaderEntry(long lifetime) { headerEntryTraces.Remove(lifetime); headerEntryPending.Remove(lifetime); }
}
