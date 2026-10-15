using BepInEx;
using GCMod.Nextgen;
using GCMod.Patches;
using GCMod.Services;
using TMPro;
using Utility.Assets;
namespace GCMod.Candidate;

/// <summary>Operational local donor lifecycle, without remote/update/hotkey/operator initialization.</summary>
public sealed class LegacyTranslationRuntime : IDisposable
{
    TranslationPackageSnapshot selected;
    readonly LocalPackageTranslationStore local;
    bool initialized;
    bool uninstalled;
    bool disposed;
    int mainThread;
    static LegacyTranslationRuntime current;
    public IMasterTranslationView MasterView { get; }
    public object SessionIdentity => Plugin.Trans?.SessionIdentity;
    public bool IsChoiceBusy => false;
    public bool CanUnload => !MasterDataPatch.IsBusy;
    public bool ContainsOutput(string table, string field, string exactKo) => ((IActiveMasterOutputView)MasterView).ContainsOutput(table, field, exactKo);
    public static bool CanAccessNative => current is { initialized: true, disposed: false }
        && Environment.CurrentManagedThreadId == current.mainThread;
    public LegacyTranslationRuntime(TranslationPackageSnapshot snapshot)
    {
        selected = snapshot;
        local = new(snapshot, Config.TranslationLanguage?.Value ?? "ko");
        MasterView = new ActiveMasterView(this);
    }
    public void Initialize()
    {
        if (initialized) return;
        if (disposed) throw new ObjectDisposedException(nameof(LegacyTranslationRuntime));
        mainThread = Environment.CurrentManagedThreadId; current = this;
        string fontPath = Config.FontBundlePath.Value;
        string absoluteFont = Path.IsPathRooted(fontPath) ? fontPath : Path.Combine(Paths.PluginPath, fontPath);
        Plugin.Trans = new TranslationManager(() => local, new AssetBundleLoader<TMP_FontAsset>(absoluteFont));
        MasterDataPatch.JsonRewriter = (attribute, json) => attribute == null ? json : Plugin.Trans.TranslateMasterData(attribute.Object, json);
        initialized = true;
        try { PatchManager.Initialize(); Plugin.Trans.Initialize(); }
        catch
        {
            if (TryUninstall()) Dispose();
            throw;
        }
    }
    public void SelectUi(TranslationPackageSnapshot snapshot)
    {
        if (snapshot.Origin != local.Origin || snapshot.ComponentSignatures["master"] != local.AuthorityIdentity
            || new[] { "master", "names", "novel" }.Any(x => snapshot.ComponentSignatures[x] != selected.ComponentSignatures[x]))
            throw new InvalidOperationException("LEGACY_AUTHORITY_CHANGE_RESTART_REQUIRED");
        // Preserve the actual manager, parser, Novel tasks and Names cache across UI-only commits.
        selected = snapshot;
    }
    public bool TryUninstall()
    {
        if (uninstalled || !initialized) return true;
        try { if (!PatchManager.Uninstall()) return false; }
        catch { Logger.Warn("LEGACY_HOOK_UNINSTALL_RETRYABLE"); return false; }
        uninstalled = true;
        return true;
    }
    public void Dispose()
    {
        if (disposed) return;
        if (!uninstalled && initialized) throw new InvalidOperationException("MASTER_CALLBACK_BUSY_OR_HOOKS_STILL_INSTALLED");
        disposed = true;
        MasterDataPatch.JsonRewriter = null;
        Plugin.Trans?.Dispose(); Plugin.Trans = null;
        if (ReferenceEquals(current, this)) current = null;
    }
    sealed class ActiveMasterView(LegacyTranslationRuntime runtime) : IMasterTranslationView, IActiveMasterOutputView
    {
        public bool ContainsOutput(string table, string field, string exactKo)
        {
            var snapshot = runtime.selected;
            return !runtime.disposed && runtime.initialized && Config.Translation.Value && snapshot.Origin == runtime.local.Origin
                && snapshot.ComponentSignatures["master"] == runtime.local.AuthorityIdentity && Plugin.Trans?.TryGetMaster(out var translator) == true
                && translator.ContainsOutput(table, field, exactKo, Config.MasterDataTables.Value);
        }
        public bool TryResolve(MasterRef reference, string role, out MasterValue value)
        {
            var snapshot = runtime.selected;
            value = new("", "active-catalog-unavailable", snapshot.Generations.Master);
            if (runtime.disposed || !runtime.initialized || string.IsNullOrEmpty(role) || !Config.Translation.Value
                || snapshot.Origin != runtime.local.Origin || snapshot.ComponentSignatures["master"] != runtime.local.AuthorityIdentity
                || Plugin.Trans?.TryGetMaster(out var translator) != true
                || !translator.TryResolve(reference.Table, reference.Field, reference.ExactJp, Config.MasterDataTables.Value, out var ko)) return false;
            value = new(ko, snapshot.Origin + ":" + snapshot.Revision + ":" + runtime.local.AuthorityIdentity, snapshot.Generations.Master);
            return true;
        }
    }
}
