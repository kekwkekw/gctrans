#nullable enable
using GCMod.Nextgen;

namespace GCMod.Candidate.Native;

public sealed partial class GeneralUiProvider
{
    // A callback-qualified activation observation is not a write lease or an owner queue.
    // It contains current Unity owners/getters and pure identities; no TMP/model producer
    // callback is retained/replayed. Inactive observations never read TMP/source/geometry.
    sealed record ProfileInfoActivation(Gc.UnitScene.UnitProfileController Profile, Gc.LabelTab Label,
        Gc.UnitScene.UnitProfileTabView Tabs, Gc.UnitScene.UnitProfileView View,
        Gc.UnitScene.UnitProfileInfoView Info, int ProfileInstance, int LabelInstance,
        int TabsInstance, int ViewInstance, int InfoInstance, long Unit, int Scene,
        string Source, int Started, long Lifetime, long UiGeneration, int ActiveFrames = 0);
    readonly Dictionary<long, ProfileInfoActivation> profileInfoActivations = new();

    bool AcceptedProfileSource(string source) => host.Snapshot.Bindings.Values.Any(b =>
        b.Enabled && b.AdapterId == "ui.caption.v1" && b.Selector.ScopeKey == "gc.labeltab" &&
        b.Selector.SlotKey == "profile_tab" && b.SourceIdentity.ExactJp == source);

    void QueueProfileInfoFromCurrentTab(Gc.LabelTab label, Gc.UnitScene.UnitProfileController? profile,
        string source, bool callbackActive)
    {
        gate.Require();
        if (!callbackActive || !Alive(profile) || Ptr(profile!._userUnit) == 0 || !Alive(profile._tab)) return;
        var tabs = profile._tab; var turner = tabs._tabController?.PageTurner; var collection = turner?.tabs;
        if (!Alive(turner) || collection == null || collection.Count > 8) return;
        bool unique = collection.ToArray().Count(x => Ptr(x) == Ptr(label)) == 1;
        var view = tabs._info; var info = view?._profileInfoView;
        bool route = Alive(view) && Alive(info) && CurrentProfileInfoRoute(info!);
        if (!ProfileInfoRebindPolicy.Eligible(callbackActive, label.isSelecting,
            AcceptedProfileSource(source), unique, route)) return;
        long key = Ptr(profile);
        // A repeated setup callback for the same live source must not extend its deadline.
        if (profileInfoActivations.TryGetValue(key, out var old) && old.Unit == Ptr(profile._userUnit) &&
            old.Source == source && old.UiGeneration == host.Snapshot.Generations.Ui &&
            old.ProfileInstance == profile.GetInstanceID() && old.TabsInstance == tabs.GetInstanceID() &&
            old.ViewInstance == view!.GetInstanceID() && old.LabelInstance == label.GetInstanceID() &&
            old.InfoInstance == info!.GetInstanceID() && old.Scene == profile.gameObject.scene.handle) return;
        if (profileInfoActivations.Count >= 8 && !profileInfoActivations.ContainsKey(key)) return;
        if (old != null) geometryDiagnostics.ForgetOwner(old.Lifetime);
        var pending = new ProfileInfoActivation(profile, label, tabs, view!, info!, profile.GetInstanceID(),
            label.GetInstanceID(), tabs.GetInstanceID(), view!.GetInstanceID(), info!.GetInstanceID(),
            Ptr(profile._userUnit), profile.gameObject.scene.handle, source, frame, gate.NextLifetime(), host.Snapshot.Generations.Ui);
        profileInfoActivations[key] = pending;
        RecordProfileInfoActivation(pending, ProfileInfoActivationAction.WaitInactive, "REQUESTED");
    }

    bool ProfileInfoActivationCurrent(ProfileInfoActivation t, out uint reason)
    {
        // Only lifetime/model/current getter/selected-tab identity reads occur here.
        // Native label text, localizer, mesh, font, glyph and full subtree checks wait
        // until the current info view has actually activated.
        reason = 0;
        if (t.UiGeneration != host.Snapshot.Generations.Ui) { reason = 1; return false; }
        if (!Alive(t.Profile) || t.Profile.GetInstanceID() != t.ProfileInstance || t.Profile.gameObject.scene.handle != t.Scene)
        { reason = 2; return false; }
        if (!t.Profile.gameObject.activeInHierarchy || Ptr(t.Profile._userUnit) != t.Unit) { reason = 4; return false; }
        if (!Alive(t.Label) || t.Label.GetInstanceID() != t.LabelInstance || !t.Label.isSelecting) { reason = 8; return false; }
        if (!Alive(t.Tabs) || t.Tabs.GetInstanceID() != t.TabsInstance || Ptr(t.Profile._tab) != Ptr(t.Tabs))
        { reason = 16; return false; }
        if (!Alive(t.View) || t.View.GetInstanceID() != t.ViewInstance || Ptr(t.Tabs._info) != Ptr(t.View))
        { reason = 32; return false; }
        if (!Alive(t.Info) || t.Info.GetInstanceID() != t.InfoInstance || Ptr(t.View._profileInfoView) != Ptr(t.Info))
        { reason = 64; return false; }
        var turner = t.Tabs._tabController?.PageTurner; var collection = turner?.tabs;
        if (!Alive(turner) || collection == null || collection.Count > 8 ||
            collection.ToArray().Count(x => Ptr(x) == Ptr(t.Label)) != 1) { reason = 128; return false; }
        if (!t.Label.transform.IsChildOf(t.Tabs.transform) || !t.View.transform.IsChildOf(t.Tabs.transform) ||
            !t.Info.transform.IsChildOf(t.View.transform)) { reason = 256; return false; }
        return true;
    }

    void TickProfileInfoActivations()
    {
        gate.Require();
        foreach (var pair in profileInfoActivations.ToArray())
        {
            var t = pair.Value;
            try
            {
                bool current = ProfileInfoActivationCurrent(t, out uint reason);
                bool active = current && t.Info.gameObject.activeInHierarchy;
                t = t with { ActiveFrames = active ? t.ActiveFrames + 1 : 0 };
                var action = ProfileInfoActivationPolicy.Assess(frame - t.Started, current, active, t.ActiveFrames);
                if (action is ProfileInfoActivationAction.WaitInactive or ProfileInfoActivationAction.WaitActive)
                { profileInfoActivations[pair.Key] = t; RecordProfileInfoActivation(t, action); continue; }
                if (action == ProfileInfoActivationAction.Activate &&
                    (t.Label.Title != t.Source || !AcceptedProfileSource(t.Source) || !CurrentProfileInfoRoute(t.Info)))
                { action = ProfileInfoActivationAction.Invalid; reason = 512; }
                if (action == ProfileInfoActivationAction.Activate)
                {
                    // Fresh scheduling begins after activation; Process performs all current
                    // exact target/localizer/source/geometry/font/control proofs before writing.
                    Queue(t.Info);
                    if (owners.TryGetValue(Ptr(t.Info), out var queued) && queued.Instance == t.InfoInstance)
                        RecordProfileInfoActivation(t, action, ownerGeneration: queued.Lifetime);
                    else RecordProfileInfoActivation(t, ProfileInfoActivationAction.Invalid, reason: 2048);
                }
                else RecordProfileInfoActivation(t, action, reason: reason);
            }
            catch { RecordProfileInfoActivation(t, ProfileInfoActivationAction.Invalid, "READFAILED", 1024); }
            profileInfoActivations.Remove(pair.Key);
            geometryDiagnostics.ForgetOwner(t.Lifetime);
        }
    }

    void RecordProfileInfoActivation(ProfileInfoActivation t, ProfileInfoActivationAction action, string? phase = null, uint reason = 0, long ownerGeneration = 0)
    {
        var failure = action == ProfileInfoActivationAction.Invalid ? UiVisualFailure.SourceChanged :
            action == ProfileInfoActivationAction.Expired ? UiVisualFailure.TargetInactive : UiVisualFailure.None;
        if (!geometryDiagnostics.TryRecord(new("profile-info-activation", Ptr(t.Info), t.Lifetime, t.UiGeneration),
            UiVisualPhase.SourceJoin, failure, UiVisualField.Target, ((uint)action + (phase == null ? 16384u : 32768u)) | (reason << 16))) return;
        string code = phase ?? action.ToString().ToUpperInvariant();
        log.Record($"PROFILE_INFO_{code}_R{reason:X}_G{t.Lifetime}_O{ownerGeneration}_U{t.UiGeneration}_A{Math.Clamp(frame-t.Started,0,999)}");
    }
    void CancelProfileInfoActivation(Gc.UnitScene.UnitProfileController profile)
    {
        if (!profileInfoActivations.Remove(Ptr(profile), out var t)) return;
        RecordProfileInfoActivation(t, ProfileInfoActivationAction.Invalid, "CANCELLED");
        geometryDiagnostics.ForgetOwner(t.Lifetime);
    }
    void ClearProfileInfoActivations()
    {
        foreach (var t in profileInfoActivations.Values) geometryDiagnostics.ForgetOwner(t.Lifetime);
        profileInfoActivations.Clear();
    }
}
