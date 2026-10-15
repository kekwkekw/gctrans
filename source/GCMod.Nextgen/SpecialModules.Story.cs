#nullable enable
using System.Collections.Immutable;
using System.Globalization;

namespace GCMod.Nextgen;

public sealed record ChoiceSource(string ScriptId, string BranchLabel, string ExactSource, long ScriptPointer,
    long CellPointer, long ButtonPointer, long DelegatePointer);
public sealed record ChoiceProjection(UiBinding Binding, UiResource Resource, ChoiceSource Source, string Text);
public sealed record ChoicePlan(string GroupId, ImmutableArray<ChoiceProjection> Options);

/// <summary>All owned visuals are prepared before visibility changes; every failed group restores all owned masks.</summary>
public static class ChoiceGroupTransaction
{
    public static bool TryCommit(int options, Func<int,bool> prepared, Func<bool> nativeUnchanged, Action<int> show, Action rollback)
        => TryCommitDetailed(options, prepared, nativeUnchanged, show, rollback).Committed;
    public static ChoiceCommitResult TryCommitDetailed(int options, Func<int,bool> prepared, Func<bool> nativeUnchanged, Action<int> show, Action rollback,
        Action<ChoiceDisplayPhase,int>? boundary = null)
    {
        ChoiceDisplayPhase phase = ChoiceDisplayPhase.CreateOwnVisual; int ordinal = -1;
        ChoiceCommitFailure? failure = null;
        try
        {
            if (options is <1 or >32) failure = new(phase, ChoiceDisplayReason.InvalidOptionCount, ordinal, ChoiceExceptionKind.None);
            else
            {
                for (int i = 0; i < options; i++) { ordinal = i; if (!prepared(i)) { failure = new(phase, ChoiceDisplayReason.OptionNotPrepared, ordinal, ChoiceExceptionKind.None); break; } }
                if (failure == null)
                {
                    ordinal = -1; phase = ChoiceDisplayPhase.PrecommitControls; boundary?.Invoke(phase, ordinal);
                    if (!nativeUnchanged()) failure = new(phase, ChoiceDisplayReason.NativeControlsChanged, ordinal, ChoiceExceptionKind.None);
                    else
                    {
                        phase = ChoiceDisplayPhase.Show;
                        for (int i = 0; i < options; i++) { ordinal = i; boundary?.Invoke(phase, ordinal); show(i); }
                        ordinal = -1; phase = ChoiceDisplayPhase.Postcommit; boundary?.Invoke(phase, ordinal);
                        if (!nativeUnchanged()) failure = new(phase, ChoiceDisplayReason.NativeControlsChanged, ordinal, ChoiceExceptionKind.None);
                        else return new(true, null);
                    }
                }
            }
        }
        catch (Exception exception) { failure = new(phase, ChoiceDisplayDiagnostics.Reason(exception), ordinal, ChoiceDisplayDiagnostics.ExceptionKind(exception)); }
        // Rollback is attempted exactly once. Its failure is reported separately and never erases the first reason.
        try { rollback(); }
        catch (Exception cleanup) { failure = failure! with { CleanupException = ChoiceDisplayDiagnostics.ExceptionKind(cleanup) }; }
        return new(false, failure);
    }
}

/// <summary>Matches an entire current native group. A row ordinal and a baseline option count are never identities.</summary>
public static class StoryChoicePolicy
{
    /// <summary>Supported UI14 lifecycle: verified completed source producer and verified natural release;
    /// visibility is checked at the existing finite main-thread active-root boundary, not an Open hook.</summary>
    public static bool RequiredLifecycleAvailable(bool sourceProducerInstalled,bool releaseInstalled) => sourceProducerInstalled&&releaseInstalled;
    public static bool IsPlanAvailable(TranslationPackageSnapshot snapshot,ChoicePlan retained,IMasterTranslationView activeMaster)
    {
        var current=Resolve(snapshot,retained.Options.Select(x=>x.Source).ToArray(),activeMaster);
        return current!=null&&current.GroupId==retained.GroupId&&current.Options.Length==retained.Options.Length
            &&current.Options.Zip(retained.Options).All(x=>x.First.Binding.Id==x.Second.Binding.Id&&x.First.Resource.Id==x.Second.Resource.Id&&x.First.Text==x.Second.Text);
    }
    public static ChoicePlan? Resolve(TranslationPackageSnapshot snapshot, IReadOnlyList<ChoiceSource> current, IMasterTranslationView? activeMaster = null)
    {
        if (current.Count is < 1 or > 32 || current.Any(x => x.ScriptPointer == 0 || x.CellPointer == 0 || x.ButtonPointer == 0 || x.DelegatePointer == 0)
            || current.Select(x => x.CellPointer).Distinct().Count() != current.Count || current.Select(x => x.ScriptPointer).Distinct().Count() != 1
            || current.Select(x => (x.ScriptId, x.BranchLabel)).Distinct().Count() != current.Count) return null;
        var resolver = new UiResourceResolver(activeMaster ?? snapshot.Master);
        var plans = new List<ChoicePlan>();
        foreach (var group in snapshot.Bindings.Values.Where(x => x.Enabled && x.AdapterId == "story.choice.visual.v1")
            .GroupBy(x => x.Selector.GroupId, StringComparer.Ordinal))
        {
            if (string.IsNullOrEmpty(group.Key)) continue;
            var rules = group.ToArray();
            if (rules.Length != current.Count || rules.Select(x => (x.Selector.ScriptId, x.Selector.BranchLabel)).Distinct().Count() != rules.Length) continue;
            var result = ImmutableArray.CreateBuilder<ChoiceProjection>(current.Count);
            foreach (var source in current)
            {
                var matches = rules.Where(x => x.Selector.ScopeKey == "story-choice-list" && x.Selector.SlotKey == "option-label"
                    && x.Selector.ScriptId == source.ScriptId && x.Selector.BranchLabel == source.BranchLabel
                    && x.SourceIdentity.ExactJp == source.ExactSource).ToArray();
                if (matches.Length != 1) break;
                var binding = matches[0]; var resource = snapshot.Resources[binding.ResourceId];
                if (resource.Jp != source.ExactSource || !resolver.TryResolve(resource, out var text)) break;
                result.Add(new(binding, resource, source, text));
            }
            if (result.Count == current.Count) plans.Add(new(group.Key!, result.ToImmutable()));
        }
        return plans.Count == 1 ? plans[0] : null;
    }
}

public sealed record AnnotationClickProof(long Generation, long Receiver, long Parent, int ReceiverInstance,
    int Thread, string Token, long Started, bool CurrentStory);
public sealed record AnnotationRecordProof(AnnotationClickProof Click, long RecordPointer, int RecordId,
    string Keyword, string Description, string Ruby, bool DeliveredAsynchronously);
public sealed record AnnotationPopupProof(long PopupPointer, int PopupInstance, long RecordPointer,
    int RecordId, string Keyword, string Description, string Ruby);
public static class AnnotationProofPolicy
{
    public static bool RecordMatches(AnnotationClickProof click, long receiver, int instance, int thread,
        long currentClickGeneration, long recordPointer, int recordId, double ageSeconds) =>
        click.Generation == currentClickGeneration && click.Receiver == receiver && click.ReceiverInstance == instance
        && click.Thread == thread && click.CurrentStory && recordPointer != 0 && ageSeconds is >= 0 and <= 3
        && click.Token == recordId.ToString(CultureInfo.InvariantCulture);
    public static bool PopupMatches(AnnotationRecordProof record, AnnotationPopupProof popup) =>
        popup.PopupPointer != 0 && popup.PopupInstance != 0 && popup.RecordPointer == record.RecordPointer
        && popup.RecordId == record.RecordId && popup.Keyword == record.Keyword
        && popup.Description == record.Description && popup.Ruby == record.Ruby;
    public static bool PopupIsCurrent(AnnotationPopupProof popup,long pointer,int instance) =>
        popup.PopupPointer==pointer&&popup.PopupInstance==instance;
}
