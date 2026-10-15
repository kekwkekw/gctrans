#nullable enable
using System.Collections.Immutable;

namespace GCMod.Nextgen;

public enum ViewportVisibility { Full, Partial, Outside }
public sealed record BindingFacts(string SourceIdentityJson, string ExactSource, string Role,
    string ScopeKey, string SlotKey, ImmutableArray<string> Segments, long OwnerGeneration, long SourceGeneration,
    int? Domain = null, int? SourceValue = null, int? SourceId = null, bool? Selected = null,
    ImmutableArray<string> MatchedSourceIdentities = default);
public sealed record UiEvidence(bool MainThread = false, bool CurrentOwner = false, bool UniqueTarget = false,
    bool DirectTarget = false, bool NativeControlsUnchanged = false, bool InputUntouched = false,
    bool UniqueValueJoin = false, bool CallbackActive = false, bool CurrentControllerScene = false,
    bool ProfileStateMatches = false, bool CurrentCellModel = false, bool MeshReady = false,
    bool IntrinsicOverflow = false, ViewportVisibility Visibility = ViewportVisibility.Full,
    bool CurrentModelOwnsSource = false, bool NumericModelUnchanged = false);
public sealed record UiBindContext(string BindingId, IUiTextTarget Target, BindingFacts Facts,
    UiEvidence Evidence, long ComponentGeneration, bool HostFixture = false, FontDonorHandle? FontDonor = null);
public sealed record UiApplyResult(bool Written, string Reason, string? Text = null);

public sealed class UiResourceResolver(IMasterTranslationView master)
{
    public bool TryResolve(UiResource resource, out string output)
    {
        output = "";
        if (resource.Disposition != "translate") return false;
        if (resource.Kind == "master_ref")
        {
            if (resource.MasterRef == null || !master.TryResolve(resource.MasterRef, resource.Role, out var value)) return false;
            output = value.Ko; return true;
        }
        output = resource.Ko!;
        return !string.IsNullOrEmpty(output);
    }
}

internal abstract class GeneralUiModule(string name)
{
    public string Name { get; } = name;
    public virtual string? Check(UiBinding binding, UiResource resource, UiBindContext context)
    {
        var e = context.Evidence;
        if (!e.MainThread || !e.CurrentOwner || !e.UniqueTarget || !e.DirectTarget || !e.NativeControlsUnchanged || !e.InputUntouched)
            return "CURRENT_OWNER_TARGET_CONTROL_REQUIRED";
        if (binding.Selector.Lifetime == "callback" && !e.CallbackActive) return "CALLBACK_LIFETIME_ENDED";
        return null;
    }
}
internal sealed class CoreUiModule() : GeneralUiModule("Core")
{
    public override string? Check(UiBinding binding, UiResource resource, UiBindContext context)
    {
        var reason = base.Check(binding, resource, context);
        if (reason != null) return reason;
        if (binding.AdapterId == "core.current-model.v1" && (!context.Evidence.CurrentModelOwnsSource || !context.Evidence.UniqueValueJoin)) return "CURRENT_MODEL_JOIN_REQUIRED";
        return null;
    }
}
internal sealed class ConsumersUiModule() : GeneralUiModule("Consumers")
{
    public override string? Check(UiBinding binding, UiResource resource, UiBindContext context)
    {
        var reason = base.Check(binding, resource, context);
        if (reason != null) return reason;
        if (binding.AdapterId == "common.date-caption.v1")
        {
            // Only complete finite source literals are admitted. No storage getter, calendar parsing, or arbitrary date format.
            if (!context.Evidence.CurrentControllerScene || resource.Kind != "literal") return "DATE_CURRENT_DIRECT_LITERAL_REQUIRED";
            if (binding.Selector.TemplateId != "last-update-empty-v1" || resource.Jp != "最終更新日：----/--/-- --:--"
                || !resource.Ko!.EndsWith("----/--/-- --:--", StringComparison.Ordinal))
                return "UNSUPPORTED_DATE_SOURCE_OR_OPAQUE_STAMP";
            return null;
        }
        if (!context.Evidence.UniqueValueJoin || !context.Evidence.CurrentModelOwnsSource) return "UNIQUE_FILTER_MODEL_VALUE_JOIN_REQUIRED";
        if (binding.Selector.SourceId != null && (binding.Selector.Lifetime != "callback" || !context.Evidence.CallbackActive)) return "TEMPORARY_SORT_CALLBACK_ONLY";
        return null;
    }
}
internal sealed class ChromeUiModule() : GeneralUiModule("Chrome")
{
    public override string? Check(UiBinding binding, UiResource resource, UiBindContext context) =>
        base.Check(binding, resource, context) ?? (context.Evidence.CurrentControllerScene ? null : "CURRENT_CONTROLLER_SCENE_REQUIRED");
}
internal sealed class ResidualsUiModule() : GeneralUiModule("Residuals")
{
    public override string? Check(UiBinding binding, UiResource resource, UiBindContext context)
    {
        var reason = base.Check(binding, resource, context);
        if (reason != null) return reason;
        if (!context.Evidence.CurrentControllerScene) return "CURRENT_CONTROLLER_SCENE_REQUIRED";
        if (binding.Selector.Type == "profile" && !context.Evidence.ProfileStateMatches) return "CURRENT_PROFILE_STATE_REQUIRED";
        return null;
    }
}
internal sealed class CaptionsUiModule() : GeneralUiModule("Captions")
{
    public override string? Check(UiBinding binding, UiResource resource, UiBindContext context)
    {
        var reason = base.Check(binding, resource, context);
        if (reason != null) return reason;
        var e = context.Evidence;
        if (!e.CurrentControllerScene) return "CURRENT_CONTROLLER_SCENE_REQUIRED";
        if (binding.Selector.Type == "profile" && !e.ProfileStateMatches) return "CURRENT_PROFILE_STATE_REQUIRED";
        if (binding.Selector.Type == "exchange" && (!e.CurrentCellModel || !e.CurrentModelOwnsSource || !e.NumericModelUnchanged)) return "CURRENT_EXCHANGE_CELL_MODEL_REQUIRED";
        // Viewport clipping and natural profile highlight changes are not intrinsic overflow or input corruption.
        if (binding.Selector.OverflowPolicy == "intrinsic-only" && (!e.MeshReady || e.IntrinsicOverflow)) return "INTRINSIC_MESH_NOT_READY_OR_OVERFLOW";
        return null;
    }
}

/// <summary>Five internal typed modules over one snapshot. No disk/network I/O and no native hook installation.</summary>
public sealed class UiModuleHost
{
    readonly object gate = new();
    readonly AdapterRegistry registry;
    readonly UiOwnershipService ownership;
    readonly SafeDiagnostics log;
    readonly FontService? fonts;
    readonly IMasterTranslationView? masterOverride;
    TranslationPackageSnapshot snapshot;
    UiResourceResolver resolver;
    readonly Dictionary<string, GeneralUiModule> modules = new(StringComparer.Ordinal)
    {
        ["Core"] = new CoreUiModule(), ["Consumers"] = new ConsumersUiModule(), ["Chrome"] = new ChromeUiModule(),
        ["Residuals"] = new ResidualsUiModule(), ["Captions"] = new CaptionsUiModule()
    };
    public UiModuleHost(TranslationPackageSnapshot snapshot, AdapterRegistry registry, UiOwnershipService ownership,
        SafeDiagnostics? diagnostics = null, IMasterTranslationView? masterOverride = null, FontService? fonts = null)
    {
        this.snapshot = snapshot; this.registry = registry; this.ownership = ownership;
        log = diagnostics ?? new(); this.masterOverride = masterOverride; this.fonts = fonts;
        resolver = new(masterOverride ?? snapshot.Master); ownership.OpenEpoch(snapshot.Generations.Ui);
    }
    public TranslationPackageSnapshot Snapshot { get { lock (gate) return snapshot; } }
    public TransitionSummary SelectSnapshot(TranslationPackageSnapshot next)
    {
        lock (gate)
        {
            if (next.Origin != snapshot.Origin || next.Locale != snapshot.Locale) throw new InvalidOperationException("SNAPSHOT_ORIGIN_CHANGE_REQUIRES_SAFE_LOAD_BOUNDARY");
            bool sameUi = next.Generations.Ui == snapshot.Generations.Ui;
            if (sameUi && next.ComponentSignatures["ui"] != snapshot.ComponentSignatures["ui"]
                || !sameUi && (next.Generations.Ui != snapshot.Generations.Ui + 1 || next.PreviousIdentity != snapshot.Identity))
                throw new InvalidOperationException("DIVERGENT_OR_NONMONOTONIC_UI_SNAPSHOT");
            if (next.Generations.Master != snapshot.Generations.Master || next.Generations.Names != snapshot.Generations.Names || next.Generations.Novel != snapshot.Generations.Novel)
                throw new InvalidOperationException("AUTHORITY_CHANGE_REQUIRES_SAFE_LOAD_BOUNDARY");
            foreach (var component in new[] { "master", "names", "novel" })
                if (next.ComponentSignatures[component] != snapshot.ComponentSignatures[component]) throw new InvalidOperationException("DIVERGENT_AUTHORITY_SNAPSHOT");
            var summary = next.Generations.Ui == snapshot.Generations.Ui ? new TransitionSummary(0, 0, 0)
                : ownership.Transition(snapshot.Generations.Ui, next.Generations.Ui);
            if (!summary.Committed) return summary;
            // Old leases are resolved before dropping the old snapshot. Disabled old rules cannot leave our stale KO behind.
            snapshot = next; resolver = new(masterOverride ?? next.Master);
            return summary;
        }
    }
    /// <summary>Settings/catalog changes restore only unavailable resources; selection and unrelated captions survive.</summary>
    public TransitionSummary ReconcileUnavailableResources(bool translationsEnabled, bool mainThread)
    {
        lock (gate)
        {
            // Do not inspect a target or glyph provider before the caller's captured game-thread boundary.
            if (!mainThread) return new(0, 0, 0, 1, false);
            return ownership.RestoreBindings(request => !translationsEnabled
                || !snapshot.Bindings.TryGetValue(request.BindingId, out var binding) || !binding.Enabled
                || !snapshot.Resources.TryGetValue(binding.ResourceId, out var resource)
                || !resolver.TryResolve(resource, out _));
        }
    }
    public UiApplyResult TryApply(UiBindContext context)
    {
        lock (gate)
        {
            // Evidence and a live native access token are checked before target/glyph/geometry reads.
            if (!context.Evidence.MainThread || !context.Evidence.CurrentOwner)
                return new(false, "PRE_READ_MAIN_THREAD_LIFETIME_REQUIRED");
            if (context.Target is IUiTargetAccessGate access && !access.CanAccess)
                return new(false, "NATIVE_ACCESS_GATE_CLOSED");
            if (context.ComponentGeneration != snapshot.Generations.Ui) return new(false, "STALE_COMPONENT_GENERATION");
            if (!snapshot.Bindings.TryGetValue(context.BindingId, out var binding)) return new(false, "UNKNOWN_BINDING");
            if (!binding.Enabled) return new(false, "BINDING_DISABLED");
            if (!registry.CanExecute(binding, context.HostFixture)) return new(false, "OPTIONAL_ADAPTER_UNAVAILABLE");
            var descriptor = registry.Find(binding.AdapterId)!;
            if (!modules.TryGetValue(descriptor.Module, out var module)) return new(false, "MODULE_OUT_OF_NG01_SCOPE");
            var resource = snapshot.Resources[binding.ResourceId];
            var preReadReason = module.Check(binding, resource, context);
            if (preReadReason != null) return new(false, preReadReason);
            var facts = context.Facts; var selector = binding.Selector;
            if (facts.ExactSource != resource.Jp || facts.Role != resource.Role || facts.ScopeKey != selector.ScopeKey || facts.SlotKey != selector.SlotKey
                || !facts.Segments.SequenceEqual(selector.Segments, StringComparer.Ordinal)
                || selector.Domain != facts.Domain || selector.SourceValue != facts.SourceValue || selector.SourceId != facts.SourceId || selector.Selected != facts.Selected)
                return new(false, "TYPED_SOURCE_ROLE_SELECTOR_MISMATCH");
            TextTargetState target;
            try { target = context.Target.ReadState(); }
            catch { log.Record("TARGET_READ_FAILURE_ISOLATED"); return new(false, "TARGET_READ_UNAVAILABLE"); }
            if (target.SourceIdentityJson != facts.SourceIdentityJson || target.ExactSource != facts.ExactSource
                || target.OwnerGeneration != facts.OwnerGeneration || target.SourceGeneration != facts.SourceGeneration)
                return new(false, "CURRENT_SOURCE_LIFETIME_MISMATCH");
            if (binding.ActionGroup == null)
            {
                if (facts.SourceIdentityJson != binding.SourceIdentity.IdentityJson) return new(false, "SOURCE_IDENTITY_MISMATCH");
            }
            else
            {
                var expected = snapshot.Bindings.Values.Where(x => x.ActionGroup == binding.ActionGroup && x.Enabled)
                    .Select(x => x.SourceIdentity.IdentityJson).ToHashSet(StringComparer.Ordinal);
                if (facts.MatchedSourceIdentities.IsDefault || !expected.SetEquals(facts.MatchedSourceIdentities)
                    || !expected.Contains(facts.SourceIdentityJson) || !descriptor.Contract.SupportsCoMatch)
                    return new(false, "EXPLICIT_COMATCH_SOURCE_SET_REQUIRED");
            }
            var reason = module.Check(binding, resource, context);
            if (reason != null) return new(false, reason);
            if (!resolver.TryResolve(resource, out var text)) return new(false, resource.Kind == "master_ref" ? "MASTER_REF_UNAVAILABLE" : "RESOURCE_KEEPS_SOURCE");
            // Native execution always needs the explicit approved donor. Optional font-less fixtures cannot lower that requirement.
            if (!descriptor.HostOnly && fonts == null || fonts != null && (context.FontDonor == null || !fonts.Supports(text, context.FontDonor)))
                return new(false, "APPROVED_FONT_GLYPHS_UNAVAILABLE");
            var result = ownership.TryWrite(context.Target, new(module.Name, binding.Id, binding.ActionGroup, selector.SlotKey,
                snapshot.Generations.Ui, target), text);
            if (result.Written) log.Record("MODULE_DISPLAY_APPLIED");
            return new(result.Written, result.Reason, text);
        }
    }
    public bool BeforeCellReuse(PhysicalTargetIdentity identity) => ownership.RestoreTarget(identity);
}
