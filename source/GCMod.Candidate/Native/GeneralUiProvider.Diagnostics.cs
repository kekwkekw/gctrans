#nullable enable
using System.Reflection;
using GCMod.Nextgen;

namespace GCMod.Candidate.Native;
public sealed partial class GeneralUiProvider
{
    readonly HashSet<string> coreProducerReports = new(StringComparer.Ordinal);
    void ReportCoreProducer(Owner owner, UiBinding binding, CoreProducerObservation? observation, string phase)
    {
        if (!gate.CanAccess) return;
        bool matched = phase == "PRODUCER_READY";
        string reason = matched ? "PRODUCER_READY" : "LITERAL_OR_TYPED_DIRECT_UNAVAILABLE";
        if (observation is null) reason = "TARGET_PATH_NOT_FOUND";
        else if (!matched)
        {
            var value = observation.Identity;
            if (value.Owner == 0 || value.Target == 0) reason = "CURRENT_OWNER_TARGET_UNAVAILABLE";
            else if (!observation.OwnerOwnsTarget) reason = "TARGET_OWNER_MISMATCH";
            else if (!observation.UniqueTarget) reason = "LOCALIZER_TARGET_NOT_UNIQUE";
            else if (!observation.AreaPathMatches) reason = "TYPED_AREA_PATH_MISMATCH";
            else if (!observation.ExactSourceOrOwnedDisplay) reason = "SOURCE_NOT_CURRENT";
            else if (value.Localizer != 0 && value.LocalizerType != (binding.Producer?.LocalizerType ?? 0)) reason = "LITERAL_TYPE_MISMATCH";
            else if (value.Localizer != 0 && value.MetadataKey != (binding.Producer?.TextConfigKey ?? "")) reason = "LITERAL_METADATA_KEY_MISMATCH";
        }
        string id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
        string code = $"CORE_{id}_O{owner.Lifetime}_U{host.Snapshot.Generations.Ui}_{reason}";
        if (coreProducerReports.Count < 1024 && coreProducerReports.Add(code)) log.Record(code);
    }
    // Only hook initialization/discovery calls this; never a per-frame tree/metadata dump.
    void ReportHookDiscovery(Type owner, string member, int requestedArity, MethodInfo[] candidates)
    {
        log.Record(HookDiagnosticCodes.Discovery(owner.FullName ?? owner.Name, member, requestedArity, candidates.Length));
        foreach (var candidate in candidates.Take(8))
            foreach (string code in HookDiagnosticCodes.Signature(owner.FullName ?? owner.Name, member,
                candidate.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name).ToArray()))
                log.Record(code);
    }
}
