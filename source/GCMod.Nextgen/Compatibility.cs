#nullable enable
using System.Collections.Immutable;
using System.Text.Json;

namespace GCMod.Nextgen;

public sealed record NativeParameter(string Type, bool ByRef = false);
public sealed record NativeMethodContract(string DeclaringType, string Method, bool IsStatic, string ReturnType,
    ImmutableArray<NativeParameter> Parameters, string Abi, int GenericArity = 0, string? BackingFieldType = null);
public sealed record NativeMethodObservation(NativeMethodContract Signature, int CandidateCount,
    bool CurrentExecutableTarget, bool SupportedInteropLayout, int AliasCount,
    bool SharedBody, string NativeDeclaringClass, string Abi);
public sealed record CompatibilityAssessment(bool Available, string Reason);

public sealed record NativeMapMember(string AssemblyIdentity, int ManagedMethodToken);

/// <summary>Reads the local generator v1 map. Its tokens refer to generated managed
/// wrappers (Pass91 NewMethod), not to IL2CPP/native metadata tokens.</summary>
public sealed class NativeMethodMap
{
    readonly Dictionary<long, int> counts = new();
    readonly Dictionary<long, NativeMapMember> unique = new();
    public int Count(long rva) => counts.GetValueOrDefault(rva);
    public NativeMapMember? UniqueMember(long rva) => unique.GetValueOrDefault(rva);
    public bool MatchesUniqueWrapper(long rva, string assemblyIdentity, int managedMethodToken) =>
        UniqueMember(rva) is { } member && member.AssemblyIdentity == assemblyIdentity &&
        member.ManagedMethodToken == managedMethodToken;
    public int EntryCount { get; private set; }
    public int AssemblyCount { get; private set; }

    public static NativeMethodMap Read(Stream stream)
    {
        if (!stream.CanRead || !stream.CanSeek || stream.Length < 20)
            throw new InvalidDataException("NATIVE_ALIAS_MAP_BOUNDS");
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        try
        {
            if (reader.ReadUInt32() != 1297370453 || reader.ReadUInt32() != 1)
                throw new InvalidDataException("NATIVE_ALIAS_MAP_FORMAT");
            uint assemblies = reader.ReadUInt32(), methods = reader.ReadUInt32(), offset = reader.ReadUInt32();
            if (assemblies is 0 or > 512 || methods is 0 or > 300000 || offset < 20 ||
                offset > 20 + assemblies * 1029L || (long)offset + methods * 16L != stream.Length)
                throw new InvalidDataException("NATIVE_ALIAS_MAP_BOUNDS");
            var names = new string[assemblies];
            for (int i = 0; i < names.Length; i++)
            {
                // BinaryWriter.Write(string): bounded 7-bit length, then UTF-8 bytes.
                int length = 0;
                for (int shift = 0; ; shift += 7)
                {
                    byte value = reader.ReadByte();
                    if (shift == 28 && (value & 0xf8) != 0)
                        throw new InvalidDataException("NATIVE_ALIAS_MAP_ASSEMBLY_INVALID");
                    length |= (value & 0x7f) << shift;
                    if ((value & 0x80) == 0) break;
                    if (shift == 28) throw new InvalidDataException("NATIVE_ALIAS_MAP_ASSEMBLY_INVALID");
                }
                if (length is < 1 or > 1024 || stream.Position + length > offset)
                    throw new InvalidDataException("NATIVE_ALIAS_MAP_ASSEMBLY_INVALID");
                names[i] = new System.Text.UTF8Encoding(false, true).GetString(reader.ReadBytes(length));
                if (string.IsNullOrWhiteSpace(names[i]) || names[i].Contains('\0'))
                    throw new InvalidDataException("NATIVE_ALIAS_MAP_ASSEMBLY_INVALID");
            }
            if (stream.Position != offset) throw new InvalidDataException("NATIVE_ALIAS_MAP_BOUNDS");
            // The generator writes ALL Int64 RVAs first, then ALL (managed token, assembly index) pairs.
            // Reading alternating Int64 address/value halves the actual RVA array and treats tokens as addresses.
            var addresses = new long[methods];
            long previous = 0;
            for (int i = 0; i < addresses.Length; i++)
            {
                long address = reader.ReadInt64();
                if (address <= 0 || address < previous)
                    throw new InvalidDataException("NATIVE_ALIAS_MAP_ADDRESS_INVALID");
                addresses[i] = address; previous = address;
            }
            var map = new NativeMethodMap { EntryCount = (int)methods, AssemblyCount = (int)assemblies };
            for (int i = 0; i < addresses.Length; i++)
            {
                int token = reader.ReadInt32(), index = reader.ReadInt32();
                if ((token & unchecked((int)0xff000000)) != 0x06000000 || (token & 0x00ffffff) == 0 ||
                    index < 0 || index >= names.Length)
                    throw new InvalidDataException("NATIVE_ALIAS_MAP_MEMBER_INVALID");
                long address = addresses[i]; int count = map.counts.GetValueOrDefault(address) + 1;
                map.counts[address] = count;
                if (count == 1) map.unique[address] = new(names[index], token);
                else map.unique.Remove(address);
            }
            return map;
        }
        catch (EndOfStreamException) { throw new InvalidDataException("NATIVE_ALIAS_MAP_BOUNDS"); }
        catch (System.Text.DecoderFallbackException) { throw new InvalidDataException("NATIVE_ALIAS_MAP_ASSEMBLY_INVALID"); }
    }
}

/// <summary>Pure signature checks. It never probes or calls a native address.</summary>
public sealed class CompatibilityService
{
    public CompatibilityAssessment Assess(NativeMethodContract expected, NativeMethodObservation actual)
    {
        var a = actual.Signature;
        if (actual.CandidateCount != 1) return new(false, "NON_UNIQUE_NATIVE_TARGET");
        if (!actual.CurrentExecutableTarget) return new(false, "TARGET_NOT_CURRENT_EXECUTABLE");
        if (!actual.SupportedInteropLayout) return new(false, "UNSUPPORTED_INTEROP_LAYOUT");
        if (actual.AliasCount < 0) return new(false, "NATIVE_METHOD_MAP_COUNT_INVALID");
        if (actual.AliasCount == 0) return new(false, "NATIVE_METHOD_MAP_NO_ENTRY");
        if (actual.AliasCount > 1 || actual.SharedBody) return new(false, "SHARED_OR_ALIASED_NATIVE_BODY");
        if (actual.NativeDeclaringClass != expected.DeclaringType || a.DeclaringType != expected.DeclaringType) return new(false, "DECLARING_CLASS_MISMATCH");
        if (a.Method != expected.Method || a.IsStatic != expected.IsStatic || a.ReturnType != expected.ReturnType || a.GenericArity != expected.GenericArity || a.BackingFieldType != expected.BackingFieldType) return new(false, "METHOD_SIGNATURE_MISMATCH");
        if (a.Abi != expected.Abi || actual.Abi != expected.Abi) return new(false, "ABI_MISMATCH");
        if (!a.Parameters.SequenceEqual(expected.Parameters)) return new(false, "PARAMETER_SIGNATURE_MISMATCH");
        return new(true, "VERIFIED_COMPATIBILITY_ONLY");
    }
}

public sealed record SelectorContract(ImmutableHashSet<string> Scopes, ImmutableHashSet<string> Slots,
    ImmutableHashSet<string> Types, bool SupportsCoMatch = false,
    ImmutableHashSet<string>? LiteralPaths = null, ImmutableHashSet<int>? Domains = null,
    ImmutableHashSet<int>? SourceValues = null, ImmutableHashSet<int>? SourceIds = null,
    ImmutableHashSet<string>? TemplateIds = null, ImmutableHashSet<string>? Lifetimes = null,
    ImmutableHashSet<string>? Shapes = null)
{
    public static SelectorContract Host(IEnumerable<string> scopes, IEnumerable<string> slots,
        IEnumerable<string>? types = null, bool supportsCoMatch = false,
        IEnumerable<IEnumerable<string>>? literalPaths = null, IEnumerable<int>? domains = null,
        IEnumerable<int>? sourceValues = null, IEnumerable<int>? sourceIds = null,
        IEnumerable<string>? templateIds = null, IEnumerable<string>? lifetimes = null) => new(
        scopes.ToImmutableHashSet(StringComparer.Ordinal), slots.ToImmutableHashSet(StringComparer.Ordinal),
        (types ?? ["static", "model", "control", "date", "profile", "exchange", "caption"]).ToImmutableHashSet(StringComparer.Ordinal), supportsCoMatch,
        literalPaths?.Select(PathKey).ToImmutableHashSet(StringComparer.Ordinal), domains?.ToImmutableHashSet(), sourceValues?.ToImmutableHashSet(), sourceIds?.ToImmutableHashSet(),
        templateIds?.ToImmutableHashSet(StringComparer.Ordinal), lifetimes?.ToImmutableHashSet(StringComparer.Ordinal));
    public static string PathKey(IEnumerable<string> segments) => JsonSerializer.Serialize(segments.ToArray());
    public static string StructuralKey(string scope, string slot, IEnumerable<string> segments, string? type = null,
        int? domain = null, int? sourceValue = null, int? sourceId = null, string? templateId = null,
        string? lifetime = null, string? overflowPolicy = null, bool? selected = null) => JsonSerializer.Serialize(new
        { scope, slot, segments = segments.ToArray(), type, domain, sourceValue, sourceId, templateId, lifetime, overflowPolicy, selected });
    public static string ShapeKey(UiSelector selector) => StructuralKey(selector.ScopeKey, selector.SlotKey,
        selector.Segments, selector.Type, selector.Domain, selector.SourceValue, selector.SourceId,
        selector.TemplateId, selector.Lifetime, selector.OverflowPolicy, selector.Selected);
    public bool Supports(UiSelector selector) => Scopes.Contains(selector.ScopeKey) && Slots.Contains(selector.SlotKey)
        && (selector.Type is null || Types.Contains(selector.Type))
        && (LiteralPaths == null || LiteralPaths.Contains(PathKey(selector.Segments)))
        && (selector.Domain == null || Domains == null || Domains.Contains(selector.Domain.Value))
        && (selector.SourceValue == null || SourceValues == null || SourceValues.Contains(selector.SourceValue.Value))
        && (selector.SourceId == null || SourceIds == null || SourceIds.Contains(selector.SourceId.Value))
        && (selector.TemplateId == null || TemplateIds == null || TemplateIds.Contains(selector.TemplateId))
        && (selector.Lifetime == null || Lifetimes == null || Lifetimes.Contains(selector.Lifetime))
        && (Shapes == null || Shapes.Contains(ShapeKey(selector)));
}
public sealed record AdapterDescriptor(string Id, string Module, SelectorContract Contract, bool Available,
    bool HostOnly, string Reason, NativeMethodContract? NativeContract = null);

/// <summary>Code-owned symbolic capabilities. JSON cannot enable an unavailable adapter.</summary>
public sealed class AdapterRegistry
{
    readonly Dictionary<string, AdapterDescriptor> adapters = new(StringComparer.Ordinal);
    readonly object gate = new();
    public static AdapterRegistry CreateNg01()
    {
        var registry = new AdapterRegistry();
        foreach (var (id, module, coMatch) in new[]
        {
            ("core.static-caption.v1", "Core", true), ("ui4.static-caption.v1", "Core", true),
            ("core.current-model.v1", "Core", false), ("core.dynamic-caption.v1", "Core", false),
            ("common.model-label.v1", "Consumers", false), ("common.date-caption.v1", "Consumers", false),
            ("ui.chrome.v1", "Chrome", false), ("ui.residual-caption.v1", "Residuals", false),
            ("ui.caption.v1", "Captions", false)
        })
        {
            registry.Register(new(id, module, SelectorContract.Host([], [], supportsCoMatch: coMatch),
                false, false, "NATIVE_BINDING_NOT_CONNECTED"));
        }
        return registry;
    }
    public void RegisterHostAdapter(string id, SelectorContract contract, string module = "Core") =>
        Register(new(id, module, contract, true, true, "HOST_FIXTURE_ONLY"));
    public void RegisterNativeAdapter(string id, string module, SelectorContract contract,
        NativeMethodContract nativeContract, NativeMethodObservation observation)
    {
        var assessment = new CompatibilityService().Assess(nativeContract, observation);
        if (contract.Scopes.Count == 0 || contract.Slots.Count == 0 || contract.LiteralPaths == null)
            assessment = new(false, "MISSING_COMPILED_SELECTOR_POLICY");
        Register(new(id, module, contract, assessment.Available, false, assessment.Reason, nativeContract));
    }
    // Registration belongs to the host / eventual compiled donor integration, never the JSON parser.
    public void Register(AdapterDescriptor descriptor)
    {
        if (!ContractRules.AdapterId(descriptor.Id)) throw new ArgumentException("INVALID_ADAPTER_ID");
        lock (gate) adapters[descriptor.Id] = descriptor;
    }
    public AdapterDescriptor? Find(string id) { lock (gate) return adapters.GetValueOrDefault(id); }
    public ImmutableArray<AdapterDescriptor> Descriptors { get { lock (gate) return adapters.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToImmutableArray(); } }
    public bool CanExecute(UiBinding binding, bool hostFixture) => Find(binding.AdapterId) is { Available: true } descriptor
        && (!descriptor.HostOnly || hostFixture) && descriptor.Contract.Supports(binding.Selector)
        && (descriptor.HostOnly || (binding.Selector.Domain == null || descriptor.Contract.Domains != null)
            && (binding.Selector.SourceValue == null || descriptor.Contract.SourceValues != null)
            && (binding.Selector.SourceId == null || descriptor.Contract.SourceIds != null)
            && (binding.Selector.TemplateId == null || descriptor.Contract.TemplateIds != null)
            && (binding.Selector.Lifetime == null || descriptor.Contract.Lifetimes != null));
}
