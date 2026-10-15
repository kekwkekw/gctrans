#nullable enable
using System.Collections.Immutable;

namespace GCMod.Nextgen;

public sealed record MasterRef(string Table, string Field, string ExactJp);
public sealed record UiResource(string Id, string Role, string Kind, string Jp, string? Ko, string Disposition, MasterRef? MasterRef,
    string MarkupPolicy = "exact-tags");
// Identity is opaque ordinal JSON data, never a command, address, or reflection expression.
public sealed record SourceIdentity(string Kind, string IdentityJson, string ExactJp);
public sealed record UiSelector(string ScopeKey, string SlotKey, ImmutableArray<string> Segments,
    string? GroupId = null, string? ScriptId = null, string? BranchLabel = null,
    string? Type = null, int? Domain = null, int? SourceValue = null, int? SourceId = null,
    bool? Selected = null, string? TemplateId = null, string? Lifetime = null, string? OverflowPolicy = null);
public sealed record UiBinding(string Id, string ResourceId, string AdapterId, bool Enabled,
    SourceIdentity SourceIdentity, UiSelector Selector, string? ActionGroup);
public sealed record PackageFile(string Path, string Kind, string Sha256, long Size);
public sealed record ComponentGenerations(long Ui, long Master, long Names, long Novel);
public sealed record EpisodeTitleLayoutPolicy(string PolicyId, string FontProfile, int BaseSize,
    ImmutableArray<int> DescendingSizes, string Restore, string OverflowFailure, string ScopeKey);
public sealed record ValidationSummary(bool Valid, ImmutableArray<string> UnavailableAdapters,
    string LegacyPayloadValidationScope = "not-indexed");
public sealed record MasterValue(string Ko, string Provenance, long Generation);
public interface IMasterTranslationView
{
    bool TryResolve(MasterRef reference, string role, out MasterValue value);
}
public interface IActiveMasterOutputView { bool ContainsOutput(string table, string field, string exactKo); }

/// <summary>Already materialized exact Master catalog; this API never reads files or mutates native data.</summary>
public sealed class MasterTranslationView : IMasterTranslationView
{
    readonly ImmutableDictionary<(string Table, string Field, string Jp), string> entries;
    readonly long generation;
    readonly string provenance;
    public static MasterTranslationView Empty { get; } = new(new Dictionary<(string, string, string), string>(), 0, "unavailable");
    public MasterTranslationView(IEnumerable<KeyValuePair<(string Table, string Field, string Jp), string>> entries,
        long generation, string provenance)
    {
        this.entries = entries.ToImmutableDictionary();
        this.generation = generation;
        this.provenance = provenance;
    }
    public bool TryResolve(MasterRef reference, string role, out MasterValue value)
    {
        value = new("", provenance, generation);
        if (string.IsNullOrEmpty(role) || !entries.TryGetValue((reference.Table, reference.Field, reference.ExactJp), out var ko) || string.IsNullOrEmpty(ko)) return false;
        value = new(ko, provenance, generation);
        return true;
    }
}

/// <summary>An immutable, coherent validation result. All render lookups use only this object.</summary>
public sealed class TranslationPackageSnapshot
{
    public string Origin { get; }
    public string Revision { get; }
    public string Locale { get; }
    public string Identity { get; }
    public string? PreviousIdentity { get; }
    public ComponentGenerations Generations { get; }
    public ImmutableDictionary<string, UiResource> Resources { get; }
    public ImmutableDictionary<string, UiBinding> Bindings { get; }
    public ImmutableArray<PackageFile> Files { get; }
    public ImmutableDictionary<string, string> ComponentSignatures { get; }
    public IMasterTranslationView Master { get; }
    public ValidationSummary Validation { get; }
    public ImmutableDictionary<string, EpisodeTitleLayoutPolicy> LayoutPolicies { get; }
    public VerifiedPackageFiles Payloads { get; }
    internal TranslationPackageSnapshot(string origin, string revision, string locale, string identity,
        ComponentGenerations generations, IEnumerable<UiResource> resources, IEnumerable<UiBinding> bindings,
        ImmutableArray<PackageFile> files, ImmutableDictionary<string, string> componentSignatures,
        IMasterTranslationView master, ValidationSummary validation, string? previousIdentity = null,
        IEnumerable<EpisodeTitleLayoutPolicy>? layoutPolicies = null, VerifiedPackageFiles? payloads = null)
    {
        Origin = origin; Revision = revision; Locale = locale; Identity = identity; Generations = generations;
        Resources = resources.ToImmutableDictionary(x => x.Id, StringComparer.Ordinal);
        Bindings = bindings.ToImmutableDictionary(x => x.Id, StringComparer.Ordinal);
        Files = files; ComponentSignatures = componentSignatures; Master = master; Validation = validation;
        PreviousIdentity = previousIdentity;
        LayoutPolicies = (layoutPolicies ?? []).ToImmutableDictionary(x => x.PolicyId, StringComparer.Ordinal);
        Payloads = payloads ?? VerifiedPackageFiles.Empty;
    }
}

public sealed class PackageValidationException(string reason) : Exception(reason);
