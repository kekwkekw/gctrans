#nullable enable
namespace GCMod.Nextgen;

public interface IRuntimeMainThreadScheduler
{
    bool IsMainThread { get; }
    Task<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default);
}
public interface IRuntimeFeatureProvider : IDisposable
{
    IMasterTranslationView MasterView { get; }
    bool IsChoiceBusy { get; }
    void Initialize(TranslationPackageSnapshot snapshot, UiModuleHost host);
    void Selected(TranslationPackageSnapshot snapshot, UiModuleHost host);
    bool TryUninstall();
}
public interface IRuntimeInitializationBoundary { bool TryResetFailedInitialization(); }
public interface IRuntimeDisplayBoundary { bool TryRestoreDisplay(); }
public interface IRuntimeUnloadBoundary { bool CanUnload { get; } }
public sealed record PreparedPackage(long Request, long Lifetime, string? BasedOnIdentity, TranslationPackageSnapshot Snapshot);
public sealed record RuntimeCommitResult(bool Committed, string Reason, TranslationPackageSnapshot? Snapshot,
    TransitionSummary? Ownership = null, bool Persisted = false);

/// <summary>Worker preparation never changes selection. One game-thread commit selects package and writer together.</summary>
public sealed class RuntimeCoordinator : IDisposable
{
    readonly PackageLoader loader;
    readonly AdapterRegistry registry;
    readonly UiOwnershipService ownership;
    readonly IRuntimeMainThreadScheduler scheduler;
    readonly IRuntimeFeatureProvider features;
    readonly LocalImmutablePackageStore? store;
    readonly FontService? fonts;
    readonly SafeDiagnostics diagnostics;
    readonly CancellationTokenSource cancellation = new();
    long request;
    readonly long lifetime = DateTime.UtcNow.Ticks;
    bool disposed;
    bool failedInitialization;
    public bool HasImmutableStore => store != null;
    public UiModuleHost? Host { get; private set; }
    public TranslationPackageSnapshot? Selected => Host?.Snapshot;
    public PreparedPackage? RestartRequired { get; private set; }
    public RuntimeCoordinator(PackageLoader loader, AdapterRegistry registry, UiOwnershipService ownership,
        IRuntimeMainThreadScheduler scheduler, IRuntimeFeatureProvider features,
        LocalImmutablePackageStore? store = null, FontService? fonts = null, SafeDiagnostics? diagnostics = null)
    {
        this.loader = loader; this.registry = registry; this.ownership = ownership; this.scheduler = scheduler;
        this.features = features; this.store = store; this.fonts = fonts; this.diagnostics = diagnostics ?? new();
    }
    public async Task<PreparedPackage?> PrepareAsync(string root, CancellationToken cancellationToken = default)
        => await PrepareCoreAsync(root, null, cancellationToken).ConfigureAwait(false);
    // Same preparation epoch/cancellation boundary. Live transport supplies only a store content ID.
    public async Task<PreparedPackage?> PrepareStoredUiAsync(string contentId, CancellationToken cancellationToken = default)
        => await PrepareCoreAsync(null, contentId, cancellationToken).ConfigureAwait(false);
    async Task<PreparedPackage?> PrepareCoreAsync(string? root, string? contentId, CancellationToken cancellationToken)
    {
        if (disposed) return null;
        var serial = Interlocked.Increment(ref request);
        var previous = Selected;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token, cancellationToken);
        try
        {
            TranslationPackageSnapshot snapshot;
            if (contentId != null)
                snapshot = (store == null ? null : await store.LoadContentAsync(contentId, previous, linked.Token).ConfigureAwait(false))?.Snapshot
                    ?? throw new PackageValidationException("UI_REQUEST_COMPLETE_PACKAGE_REQUIRED");
            else snapshot = store == null
                ? await loader.LoadAsync(root!, previous: previous, cancellationToken: linked.Token).ConfigureAwait(false)
                : (await store.StageAsync(root!, previous, linked.Token).ConfigureAwait(false)).Snapshot;
            if (disposed || serial != Volatile.Read(ref request) || linked.IsCancellationRequested) return null;
            return new(serial, lifetime, previous?.Identity, snapshot);
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException
            or PackageValidationException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { diagnostics.Record("CANDIDATE_REJECTED_SELECTION_RETAINED"); return null; }
    }
    public bool TryGetValidatedReusePath(PackageFile descriptor, out string path)
    {
        path = "";
        return Selected is { } snapshot && store != null && store.TryGetValidatedReusePath(snapshot, descriptor, out path);
    }
    public async Task<TranslationPackageSnapshot?> RecoverPreparationBaselineAsync(CancellationToken cancellationToken = default)
    {
        if (disposed) return null;
        return Selected ?? (store == null ? null : (await store.RecoverAsync(cancellationToken).ConfigureAwait(false))?.Snapshot);
    }
    public bool TryGetValidatedReusePath(TranslationPackageSnapshot snapshot, PackageFile descriptor, out string path)
    {
        path = ""; return store != null && store.TryGetValidatedReusePath(snapshot, descriptor, out path);
    }
    public bool SavePendingStartup(PreparedPackage prepared, string? basedOnIdentity = null)
    {
        if (disposed || store == null || prepared.Lifetime != lifetime || prepared.Request != Volatile.Read(ref request)) return false;
        try { store.RecordPendingRestart(prepared.Snapshot, basedOnIdentity ?? Selected?.Identity, "OFFLINE_PREPARED_STARTUP"); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PackageValidationException)
        { diagnostics.Record("PENDING_STARTUP_WRITE_FAILED_CURRENT_RETAINED"); return false; }
    }
    public async Task<RuntimeCommitResult> StartAsync(string root, CancellationToken cancellationToken = default)
    {
        if (Host != null) return new(false, "ALREADY_STARTED", Selected);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token, cancellationToken);
        try
        {
            bool clean = await scheduler.InvokeAsync(TryCleanupFailedInitialization, linked.Token).ConfigureAwait(false);
            if (!clean) return new(false, "INITIALIZATION_CLEANUP_BUSY_EXPLICIT_RETRY_REQUIRED", Selected);
            // Managed selection is the startup authority. PackageRoot is bootstrap or explicit reload only.
            var current = store == null ? null : await store.RecoverAsync(linked.Token).ConfigureAwait(false);
            var pendingRecord = store?.ReadPendingRestart();
            var pending = store == null ? null : await store.RecoverPendingAsync(linked.Token).ConfigureAwait(false);
            if (pendingRecord != null && pending == null)
                try { store?.RejectPendingRestart("PENDING_PACKAGE_INVALID_LKG_RETAINED"); } catch { diagnostics.Record("PENDING_REJECTION_WRITE_FAILED"); }
            if (pending != null && current != null && pendingRecord?.PreviousContentId is { } expectedBase && expectedBase != current.ContentId)
            {
                // A UI commit after preparation must not be reverted by an older complete restart package.
                try { store?.RejectPendingRestart("PENDING_BASE_CHANGED_LKG_RETAINED"); } catch { diagnostics.Record("PENDING_REJECTION_WRITE_FAILED"); }
                diagnostics.Record("PENDING_BASE_CHANGED_LKG_RETAINED"); pending = null;
            }
            PreparedPackage? prepared = pending != null || current != null
                ? new(Interlocked.Increment(ref request), lifetime, null, (pending ?? current ?? throw new InvalidOperationException("RECOVERY_PACKAGE_REQUIRED")).Snapshot)
                : await PrepareAsync(root, linked.Token).ConfigureAwait(false);
            if (prepared == null) return new(false, "NO_VALID_LOCAL_PACKAGE_OR_LKG", Selected);
            var result = await scheduler.InvokeAsync(() => CommitUi(prepared), linked.Token).ConfigureAwait(false);
            if (pending != null && !result.Committed && result.Reason == "INITIALIZATION_FAILED_RETRYABLE")
            {
                bool reset = await scheduler.InvokeAsync(TryCleanupFailedInitialization, linked.Token).ConfigureAwait(false);
                if (!reset) return new(false, "INITIALIZATION_CLEANUP_BUSY_LKG_DEFERRED", Selected);
                try { store?.RejectPendingRestart("PENDING_INITIALIZATION_FAILED_LKG_RETAINED"); } catch { diagnostics.Record("PENDING_REJECTION_WRITE_FAILED"); }
                if (current != null)
                {
                    var lkg = new PreparedPackage(Interlocked.Increment(ref request), lifetime, null, current.Snapshot);
                    return await scheduler.InvokeAsync(() => CommitUi(lkg), linked.Token).ConfigureAwait(false);
                }
            }
            return result;
        }
        catch (OperationCanceledException) { return new(false, "LIFETIME_CANCELLED", Selected); }
    }
    bool TryCleanupFailedInitialization()
    {
        if (!scheduler.IsMainThread) return false;
        if (!failedInitialization) return true;
        bool reset;
        try { reset = features is IRuntimeInitializationBoundary boundary ? boundary.TryResetFailedInitialization() : features.TryUninstall(); }
        catch { diagnostics.Record("INITIALIZATION_CLEANUP_FAILURE_OWNERSHIP_RETAINED"); return false; }
        if (reset) failedInitialization = false;
        return reset;
    }
    public static bool RequiresRestart(TranslationPackageSnapshot old, TranslationPackageSnapshot next) =>
        next.Origin != old.Origin || next.Locale != old.Locale ||
        new[] { "master", "names", "novel" }.Any(c => old.ComponentSignatures[c] != next.ComponentSignatures[c]);
    public RuntimeCommitResult CommitUi(PreparedPackage prepared) => CommitCore(prepared, false);
    public RuntimeCommitResult CommitUiRequest(PreparedPackage prepared) => CommitCore(prepared, true);
    RuntimeCommitResult CommitCore(PreparedPackage prepared, bool uiRequestOnly)
    {
        if (!scheduler.IsMainThread) return new(false, "MAIN_THREAD_REQUIRED", Selected);
        if (disposed || cancellation.IsCancellationRequested || prepared.Lifetime != lifetime
            || prepared.Request != Volatile.Read(ref request)) return new(false, "STALE_OR_CANCELLED_PREPARATION", Selected);
        var old = Selected;
        if (prepared.BasedOnIdentity != old?.Identity) return new(false, "SELECTION_CHANGED_SINCE_PREPARE", old);
        var next = prepared.Snapshot;
        if (old?.Identity == next.Identity) return new(false, "SAME_SELECTION_NOOP", old);
        if (uiRequestOnly && old == null) return new(false, "UI_REQUEST_ACTIVE_SELECTION_REQUIRED", old);
        if (old != null && RequiresRestart(old, next))
        {
            // Data-only live requests must never overwrite an unrelated pending restart selection.
            if (uiRequestOnly) return new(false, "RESTART_REQUIRED", old);
            RestartRequired = prepared;
            bool pendingPersisted = false;
            if (store != null)
                try { store.RecordPendingRestart(next, old.Identity, "AUTHORITY_CHANGE_RESTART_REQUIRED"); pendingPersisted = true; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PackageValidationException) { diagnostics.Record("PENDING_RESTART_WRITE_FAILED_CURRENT_RETAINED"); }
            return new(false, "RESTART_REQUIRED", old, Persisted: pendingPersisted);
        }
        if (features.IsChoiceBusy) return new(false, "CHOICE_SAFE_BOUNDARY_DEFERRED", old);
        if (old != null && features is IRuntimeDisplayBoundary boundary && !boundary.TryRestoreDisplay())
            return new(false, "NATIVE_DISPLAY_RESTORE_DEFERRED", old);
        if (failedInitialization) return new(false, "INITIALIZATION_CLEANUP_BUSY_EXPLICIT_RETRY_REQUIRED", old);
        TransitionSummary? summary = null;
        if (Host == null)
        {
            var host = new UiModuleHost(next, registry, ownership, diagnostics, features.MasterView, fonts);
            try { features.Initialize(next, host); }
            catch
            {
                // An explicit retry must first uninstall any partial hooks; busy callbacks retain ownership.
                failedInitialization = true;
                _ = TryCleanupFailedInitialization();
                diagnostics.Record("INITIALIZATION_FAILED_RETRYABLE");
                return new(false, "INITIALIZATION_FAILED_RETRYABLE", old);
            }
            Host = host;
        }
        else
        {
            summary = Host.SelectSnapshot(next);
            if (!summary.Committed) return new(false, "OWNERSHIP_RESTORE_DEFERRED", old, summary);
            features.Selected(next, Host);
        }
        bool persisted = false;
        if (store != null)
        {
            try { store.Select(next); persisted = true; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PackageValidationException)
            { diagnostics.Record("PERSISTENT_SELECTION_WRITE_FAILED_PREVIOUS_LKG_RETAINED"); }
        }
        if (persisted && store != null && !uiRequestOnly)
            try { store.CompletePendingRestart(next.Identity); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { diagnostics.Record("PENDING_CLEAR_WRITE_FAILED_CURRENT_PERSISTED"); }
        diagnostics.Record("SINGLE_RUNTIME_SELECTION_COMMITTED");
        return new(true, "RUNTIME_SELECTION_COMMITTED", Selected, summary, persisted);
    }
    public bool TryUnload()
    {
        if (disposed) return true;
        if (!scheduler.IsMainThread) return false;
        if (features is IRuntimeUnloadBoundary unload && !unload.CanUnload) return false;
        if (features is IRuntimeDisplayBoundary boundary && !boundary.TryRestoreDisplay()) return false;
        if (Host != null)
        {
            // Restore without advancing the epoch. A newly busy Master callback may still defer uninstallation.
            var summary = ownership.RestoreAllCurrent();
            if (!summary.Committed) return false;
        }
        // Master TryUninstall is authoritative even after the preflight; no session/font disposal on failure.
        if (!features.TryUninstall()) return false;
        disposed = true; cancellation.Cancel(); Interlocked.Increment(ref request);
        features.Dispose(); cancellation.Dispose(); Host = null; return true;
    }
    public void Dispose() { _ = TryUnload(); }
}

