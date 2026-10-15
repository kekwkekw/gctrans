#nullable enable
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace GCMod.Nextgen;

/// <summary>Captured verified bytes are parsed once. Unrequested Novel payloads are never opened at startup.</summary>
public sealed class VerifiedPackageFiles
{
    readonly IPackageFileSource source;
    readonly ConcurrentDictionary<string, byte[]> captured;
    readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new(StringComparer.Ordinal);
    public string Root { get; }
    public ImmutableDictionary<string, PackageFile> Descriptors { get; }
    public static VerifiedPackageFiles Empty { get; } = new("", new LocalPackageFileSource(), [], new Dictionary<string, byte[]>());
    public VerifiedPackageFiles(string root, IPackageFileSource source, IEnumerable<PackageFile> descriptors,
        IReadOnlyDictionary<string, byte[]> captured)
    {
        Root = root; this.source = source;
        Descriptors = descriptors.ToImmutableDictionary(x => x.Path, StringComparer.Ordinal);
        this.captured = new(captured.Select(x => new KeyValuePair<string, byte[]>(x.Key, x.Value.ToArray())), StringComparer.Ordinal);
    }
    public async Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        ContractRules.SafePath(relativePath);
        if (!Descriptors.TryGetValue(relativePath, out var descriptor)) return null;
        var gate = gates.GetOrAdd(relativePath, _ => new SemaphoreSlim(1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!captured.TryGetValue(relativePath, out var bytes))
            {
                bytes = (await source.ReadAsync(Root, relativePath, cancellationToken).ConfigureAwait(false)).ToArray();
                ContractRules.Require(bytes.LongLength == descriptor.Size && PackageLoader.Hash(bytes) == descriptor.Sha256, "PACKAGE_INTEGRITY_FAILED");
                // Cold/changed Novel materialization and lazy first use share the same legacy dictionary guard.
                // Warm immutable-copy reuse does not enter this read path.
                if (descriptor.Kind is "novel" or "names" or "legacy_manifest") LegacyPayloadContracts.Validate(descriptor, bytes);
                cancellationToken.ThrowIfCancellationRequested();
                captured[relativePath] = bytes;
            }
            // A caller cannot mutate our verified byte cache.
            return bytes.ToArray();
        }
        finally { gate.Release(); }
    }
}
