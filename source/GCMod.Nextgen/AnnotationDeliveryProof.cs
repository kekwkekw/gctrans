#nullable enable
using System.Globalization;
namespace GCMod.Nextgen;

/// <summary>Actual typed factory delivery is a record source, independently of the popup instance.
/// Native callers must supply current receiver/parent identity after their game-thread/lifetime gate.</summary>
public sealed record AnnotationDeliveredRecord(long Pointer, int Id, string Keyword, string Description, string Ruby);
public sealed record AnnotationDeliverySelection(AnnotationRecordProof? Record, string Reason);
public static class AnnotationDeliveryProofPolicy
{
    public static bool RecordEligible(AnnotationClickProof click, long currentReceiver, long currentParent,
        long parentClickReceiver, int currentReceiverInstance, int currentThread, long currentClickGeneration,
        long recordPointer, int recordId, double ageSeconds, bool alreadyClaimed) =>
        !alreadyClaimed && currentReceiver != 0 && currentParent != 0 && currentParent == click.Parent &&
        parentClickReceiver == currentReceiver && AnnotationProofPolicy.RecordMatches(click, currentReceiver,
            currentReceiverInstance, currentThread, currentClickGeneration, recordPointer, recordId, ageSeconds);

    public static AnnotationDeliverySelection Select(IEnumerable<AnnotationRecordProof> eligible,
        AnnotationDeliveredRecord actual, AnnotationRecordProof? privateRecord = null)
    {
        var candidates = eligible.Take(33).ToArray();
        if (candidates.Length != 1) return new(null, candidates.Length == 0
            ? "ANNOTATION_CURRENT_CLICK_REQUIRED" : "ANNOTATION_COMPETING_CLICK_HELD");
        var record = candidates[0];
        if (actual.Pointer == 0 || record.RecordPointer != actual.Pointer || record.RecordId != actual.Id ||
            record.Keyword != actual.Keyword || record.Description != actual.Description || record.Ruby != actual.Ruby ||
            !record.Click.CurrentStory || record.Click.Token != actual.Id.ToString(CultureInfo.InvariantCulture))
            return new(null, "ANNOTATION_DELIVERED_RECORD_CHANGED");
        if (privateRecord != null)
        {
            if (privateRecord.Click != record.Click || privateRecord.RecordPointer != actual.Pointer || privateRecord.RecordId != actual.Id ||
                privateRecord.Keyword != actual.Keyword || privateRecord.Description != actual.Description || privateRecord.Ruby != actual.Ruby)
                return new(null, "ANNOTATION_PRIVATE_FACTORY_RECORD_CONFLICT_HELD");
            return new(privateRecord, "ANNOTATION_ACTUAL_TYPED_RECORD_PROVEN");
        }
        // A factory delivery alone cannot tie a late async popup to a particular prior same-ID click.
        // Preserve the accepted conservative residual unless the separately observed private delivery proved it.
        if (record.DeliveredAsynchronously) return new(null, "ANNOTATION_ASYNC_FACTORY_CLICK_UNPROVEN");
        return new(record, "ANNOTATION_ACTUAL_TYPED_RECORD_PROVEN");
    }
}
