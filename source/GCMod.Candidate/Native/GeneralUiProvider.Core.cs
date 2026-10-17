#nullable enable
using GCMod.Nextgen;
using TMPro;
using UnityEngine;

namespace GCMod.Candidate.Native;

public sealed partial class GeneralUiProvider
{
    // An OptionPopup's typed area field is the anchor; transient tab clone names are not a discovery prerequisite.
    List<Transform> CoreTargets(Owner owner, UiBinding binding)
    {
        gate.Require();
        var result = MissionCellCaptionPolicy.Supports(binding) ? MissionCellTargets(owner,binding) : binding.Producer?.OptionArea is null ? ExactPaths(owner.Value.transform, binding.Selector.Segments)
            : TryOptionAreaTarget(owner, binding, out _, out var leaf) ? new List<Transform> { leaf! } : new();
        if (result.Count == 0 && binding.AdapterId is "core.static-caption.v1" or "ui4.static-caption.v1")
            ReportCoreProducer(owner, binding, null, "CORE_TARGET_PATH_NOT_FOUND");
        return result;
    }
    bool TryOptionAreaTarget(Owner owner, UiBinding binding, out Component? area, out Transform? leaf)
    {
        gate.Require(); area = null; leaf = null;
        var path = binding.Producer?.OptionArea;
        if (!Current(owner) || path == null || owner.Value is not Gc.Option.OptionPopup option) return false;
        // Finite native fields only. JSON cannot invoke methods, reflection chains, or select another owner family.
        string nativeType;
        switch (path.Field)
        {
            case "soundAndTextOptionArea": area = option.soundAndTextOptionArea; nativeType = "Gc.Option.SoundAndTextOptionArea"; break;
            case "screenSizeOptionArea": area = option.screenSizeOptionArea; nativeType = "ScreenSizeOptionArea"; break;
            case "downloadOptionArea": area = option.downloadOptionArea; nativeType = "Gc.Option.DownloadOptionArea"; break;
            default: return false;
        }
        if (!Alive(area) || !Exact(area, nativeType) || !area!.transform.IsChildOf(option.transform)) return false;
        leaf = CoreProducerPolicy.ResolveAreaTarget(gate.CanAccess, area.transform, path,
            t => t.childCount, (t, index) => t.GetChild(index), t => t.name);
        return Alive(leaf) && leaf!.IsChildOf(area.transform);
    }
    bool CoreSourceCurrent(Owner owner, TextMeshProUGUI text, UiBinding binding)
    {
        if (text.text == binding.SourceIdentity.ExactJp) return true;
        // The KO is accepted only while this exact owner/generation/target still has our corresponding visual lease.
        return geometryOwned.Values.Any(t => CoreProducerPolicy.OwnedDisplayMayConfirmSource(t.Failed, t.RestoringText) && t.OwnerGeneration == owner.Lifetime
            && t.UiGeneration == host.Snapshot.Generations.Ui && Ptr(t.Text) == Ptr(text) && t.WrittenText == text.text
            && (t.Binding.Id == binding.Id || binding.ActionGroup != null && t.Binding.ActionGroup == binding.ActionGroup));
    }
    CoreProducerObservation ObserveCoreProducer(Owner owner, TextMeshProUGUI text, UiBinding binding)
    {
        gate.Require();
        bool live = Current(owner) && Alive(text);
        if (!live) return new(new(0, 0, 0, 0, 0, 0, -1, "", 0, 0), false, false, false, false);
        bool owns = text.transform.IsChildOf(owner.Value.transform);
        var row=MissionCellCaptionPolicy.Supports(binding)?Parent<Gc.Mission.ContentScrollCell>(text):null;
        var all = Alive(row) ? row!.GetComponentsInChildren<Gc.Localizer.TextLocalizer>(true) : owner.Value.GetComponentsInChildren<Gc.Localizer.TextLocalizer>(true);
        Require(all.Length <= 512, "CORE_OWNER_LOCALIZER_BOUND");
        var matches = all.Where(l => Alive(l) && Alive(l.text) && Ptr(l.text) == Ptr(text)).ToArray();
        bool absent = matches.Length == 0 && text.GetComponents<Gc.Localizer.TextLocalizer>().Length == 0;
        var localizer = matches.Length == 1 ? matches[0] : null;
        bool unique = text.GetComponents<TextMeshProUGUI>().Length == 1
            && (absent || localizer != null && Exact(localizer, "Gc.Localizer.TextLocalizer"));
        Component? area = null; bool pathMatches;
        if(MissionCellCaptionPolicy.Supports(binding)) pathMatches=TryMissionCellCaption(owner,binding,text,out _);
        else if (binding.Producer?.OptionArea != null)
            pathMatches = TryOptionAreaTarget(owner, binding, out area, out var leaf) && Ptr(leaf) == Ptr(text.transform);
        else
        {
            var leaves = ExactPaths(owner.Value.transform, binding.Selector.Segments);
            pathMatches = leaves.Count == 1 && Ptr(leaves[0]) == Ptr(text.transform);
        }
        var identity = new CoreProducerIdentity(Ptr(owner.Value), owner.Instance, owner.Lifetime, Ptr(text), text.GetInstanceID(),
            Ptr(localizer), localizer == null ? -1 : (int)localizer.type, localizer?.textConfigKey ?? "", Ptr(area), Alive(area) ? area!.GetInstanceID() : 0);
        return new(identity, owns, unique, CoreSourceCurrent(owner, text, binding), pathMatches);
    }
    bool TryCoreProducer(Owner owner, TextMeshProUGUI text, UiBinding binding, out CoreProducerIdentity? captured)
    {
        gate.Require(); captured = null;
        if (!Current(owner) || !Alive(text)) return false;
        var observation = ObserveCoreProducer(owner, text, binding);
        TraceHeaderEntry(owner, "PRODUCER", text, binding, observation.ExactSourceOrOwnedDisplay);
        bool absentAllowed = binding.AdapterId == "ui4.static-caption.v1" && CoreDirect(owner.Value, text, binding.Selector.Segments);
        if (!CoreProducerPolicy.Matches(gate.CanAccess, binding.Producer, absentAllowed, null, () => observation))
        {
            var i = observation.Identity;
            string reason = !observation.OwnerOwnsTarget ? "CORE_OWNER_MISMATCH"
                : !observation.UniqueTarget ? "CORE_TARGET_OR_LOCALIZER_NOT_UNIQUE"
                : !observation.ExactSourceOrOwnedDisplay ? "CORE_CURRENT_SOURCE_MISMATCH"
                : !observation.AreaPathMatches ? "CORE_CURRENT_TARGET_PATH_MISMATCH"
                : i.Localizer == 0 ? "CORE_LOCALIZER_ABSENT_UNSUPPORTED"
                : i.LocalizerType != (binding.Producer?.LocalizerType ?? 0) ? "CORE_LITERAL_TYPE_MISMATCH"
                : i.MetadataKey != (binding.Producer?.TextConfigKey ?? "") ? "CORE_LITERAL_METADATA_KEY_MISMATCH"
                : "CORE_PRODUCER_CURRENT_UNAVAILABLE";
            ReportCoreProducer(owner, binding, observation, reason);
            return false;
        }
        ReportCoreProducer(owner, binding, observation, "PRODUCER_READY");
        captured = observation.Identity;
        TrackHeaderProducer(owner, text, binding, captured);
        TrackFilterCaption(owner, text, binding, captured);
        return true;
    }
    bool CoreProducerCurrent(Owner owner, TextMeshProUGUI text, UiBinding binding, CoreProducerIdentity? captured)
    {
        if (!gate.CanAccess || captured == null || !Current(owner) || !Alive(text)) return false;
        bool absentAllowed = binding.AdapterId == "ui4.static-caption.v1" && CoreDirect(owner.Value, text, binding.Selector.Segments);
        return CoreProducerPolicy.Matches(true, binding.Producer, absentAllowed, captured, () => ObserveCoreProducer(owner, text, binding));
    }
}
