using System.Net.Http;
using System.Text.Json;
using GCMod.Nextgen;

// Explicit actual-HTTP probe for the already downloaded package in a disposable store.
// The feature provider is a CPU stub. No native/plugin lifecycle is executed or proved.
internal static class ActualBranchProbe
{
    public static async Task RunAsync(string storeRoot, string expectedCommit, string report)
    {
        static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
        var registry = AdapterRegistry.CreateNg01();
        var loader = new PackageLoader(registry);
        var store = new LocalImmutablePackageStore(storeRoot, loader);
        var pending = store.ReadPendingRestart() ?? throw new InvalidOperationException("EXISTING_VERIFIED_SCRATCH_PENDING_REQUIRED");
        var features = new CpuFeatures();
        using var coordinator = new RuntimeCoordinator(loader, registry, new UiOwnershipService(), new CpuScheduler(), features, store);
        // Start only the CPU coordinator in this explicit scratch store. This supplies a
        // real validated LKG baseline for changed-only reuse without a second corpus fetch.
        var started = await coordinator.StartAsync(Path.Combine(storeRoot, "packages", pending.ContentId));
        Require(started.Committed && coordinator.Selected != null, "CPU_SCRATCH_START_FAILED");
        var selected = coordinator.Selected!;
        byte[]? Record(string name) => File.Exists(Path.Combine(storeRoot, name)) ? File.ReadAllBytes(Path.Combine(storeRoot, name)) : null;
        var before = new[] { "current.json", "previous.json", "pending_restart.json" }.ToDictionary(n => n, Record);
        using var trace = new TraceHandler(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false });
        using var client = new HttpClient(trace) { Timeout = Timeout.InfiniteTimeSpan };
        var result = await new FixedRefDataUpdater(client, Path.Combine(storeRoot, "branch-downloads"))
            .PrepareAsync(new("kekwkekw/gctrans", "ng12/publication-candidate"), coordinator);
        bool recordsUnchanged = before.All(row => (row.Value == null && Record(row.Key) == null) ||
            row.Value != null && Record(row.Key) is { } now && row.Value.SequenceEqual(now));
        bool fixedUris = trace.Requests.All(u => u.Host == "api.github.com"
            ? u.AbsolutePath == "/repos/kekwkekw/gctrans/git/ref/heads/ng12/publication-candidate"
            : u.Host == "raw.githubusercontent.com" && u.AbsolutePath.StartsWith("/kekwkekw/gctrans/" + expectedCommit + "/translations/ko/", StringComparison.Ordinal));
        bool passed = result.Prepared != null && result.Reason == "UPDATE_NO_CHANGE" && result.TransportCommit == expectedCommit
            && result.Prepared.Snapshot.Identity == selected.Identity && ReferenceEquals(selected, coordinator.Selected)
            && recordsUnchanged && fixedUris && features.Initializations == 1 && features.Selections == 0
            && result.Metrics.PayloadDownloads == 0 && result.Metrics.ReusedFiles == selected.Payloads.Descriptors.Count
            && result.Metrics.AppPayloadReads == 0 && result.Metrics.AppPayloadHashes == 0;
        var recovered = await new LocalImmutablePackageStore(storeRoot, loader).RecoverAsync();
        passed &= recovered?.ContentId == selected.Identity;
        var output = new { status = passed ? "PASS" : "FAIL", actual_http = true,
            candidate_dll_sha256 = PackageLoader.Hash(await File.ReadAllBytesAsync(typeof(FixedRefDataUpdater).Assembly.Location)),
            result.Reason, result.TransportCommit, result.Metrics,
            expected_commit = expectedCommit, content_id = selected.Identity,
            request_uris = trace.Requests.Select(u => u.AbsoluteUri).ToArray(), all_requests_fixed_after_ref = fixedUris,
            selection_records_unchanged = recordsUnchanged, lkg_recovered = recovered?.ContentId == selected.Identity,
            CPU_features_initializations = features.Initializations, CPU_selection_callbacks = features.Selections,
            CPU_scratch_startup_only = true, native = "NOT_RUN", game = "NOT_RUN", installation = "NOT_PERFORMED" };
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(output));
        Environment.ExitCode = passed ? 0 : 1;
    }
    sealed class TraceHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Requests.Add(request.RequestUri!); return base.SendAsync(request, ct); }
    }
    sealed class CpuScheduler : IRuntimeMainThreadScheduler
    {
        public bool IsMainThread => true;
        public Task<T> InvokeAsync<T>(Func<T> callback, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); return Task.FromResult(callback()); }
    }
    sealed class CpuFeatures : IRuntimeFeatureProvider
    {
        public IMasterTranslationView MasterView => MasterTranslationView.Empty;
        public bool IsChoiceBusy => false;
        public int Initializations, Selections;
        public void Initialize(TranslationPackageSnapshot snapshot, UiModuleHost host) => Initializations++;
        public void Selected(TranslationPackageSnapshot snapshot, UiModuleHost host) => Selections++;
        public bool TryUninstall() => true;
        public void Dispose() { }
    }
}
