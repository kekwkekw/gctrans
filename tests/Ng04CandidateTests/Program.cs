using System.Reflection;
using System.Text.Json;
using GCMod.Candidate;
using GCMod.Nextgen;
using GCMod.Services;

if (args.Length is < 2 or > 3) throw new ArgumentException("candidate-translation-root report.json [chain-fixtures.json]");
var root = Path.GetFullPath(args[0]);
var tests = new List<object>();
var failures = 0;
object? compiledBattleRouteEvidence = null;
void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
async Task Check(string id, string name, Func<Task> action)
{
    try { await action(); tests.Add(new { id, name, status = "PASS", axis = "actual_candidate_DLL_portable_CPU" }); }
    catch (Exception error) { failures++; tests.Add(new { id, name, status = "FAIL", axis = "actual_candidate_DLL_portable_CPU", reason = error.ToString() }); }
}
var registry = AdapterRegistry.CreateNg01();
var snapshot = await new PackageLoader(registry).LoadAsync(root);
var local = new LocalPackageTranslationStore(snapshot);
var semantic = await local.FetchManifestAsync();
var master = await local.LoadAsync<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(TranslationPaths.Master)
    ?? throw new Exception("actual local Master store unavailable");
using var session = new TranslationSession(local);
var translator = await session.GetMasterAsync() ?? throw new Exception("actual Master translator unavailable");
var enabledRefs = snapshot.Bindings.Values.Where(b => b.Enabled).Select(b => snapshot.Resources[b.ResourceId])
    .Where(r => r.Kind == "master_ref" && r.Disposition == "translate").DistinctBy(r => r.Id).ToArray();
foreach (var resource in enabledRefs)
{
    await Check("A03", "actual_local_store_and_MasterTranslationView_" + resource.Id, () => {
        var reference = resource.MasterRef!;
        Require(resource.Ko == null, "MasterRef has literal KO override");
        var expected = master[reference.Table][reference.Field][reference.ExactJp];
        Require(snapshot.Master.TryResolve(reference, resource.Role, out var value) && value.Ko == expected, "snapshot MasterTranslationView miss");
        Require(value.Generation == snapshot.Generations.Master && !string.IsNullOrEmpty(value.Provenance), "view generation/provenance missing");
        Require(translator.TryResolve(reference.Table, reference.Field, reference.ExactJp, ["*"], out var active) && active == expected, "actual session catalog miss");
        Require(new UiResourceResolver(snapshot.Master).TryResolve(resource, out var resolved) && resolved == expected, "actual UI resolver miss");
        Require(!snapshot.Master.TryResolve(reference with { ExactJp = reference.ExactJp + " " }, resource.Role, out _), "exact JP lookup became fuzzy");
        Require(!snapshot.Master.TryResolve(reference, "", out _), "empty role resolved");
        return Task.CompletedTask;
    });
}
await Check("A03", "actual_session_and_local_semantic_authority", () => {
    Require(semantic != null, "semantic manifest missing");
    Require(session.TryGetMaster(out var ready) && ReferenceEquals(ready,translator), "actual ready translator task not reused");
    Require(local.AuthorityIdentity == snapshot.ComponentSignatures["master"], "local authority differs");
    using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"manifest.json")));
    Require(semantic!.Master == document.RootElement.GetProperty("master").GetString(), "actual local store old semantic authority");
    return Task.CompletedTask;
});

var leaves = master.SelectMany(t => t.Value.SelectMany(f => f.Value.Select(p => (Table:t.Key,Field:f.Key,Jp:p.Key,Ko:p.Value))))
    .Where(p => p.Ko.Length > 0 && p.Ko != p.Jp).ToArray();
foreach (var grammar in new[] { "scalar", "array", "delimited" })
{
    await Check("A04", "actual_MasterDataTranslator_" + grammar + "_single_and_array_records", () => {
        var leaf = leaves.First(p => grammar == "array" ? p.Field.EndsWith("[]") : grammar == "delimited" ? p.Field.EndsWith('|') : !p.Field.EndsWith("[]") && !p.Field.EndsWith('|'));
        var physical = grammar == "array" ? leaf.Field[..^2] : grammar == "delimited" ? leaf.Field[..^1] : leaf.Field;
        var suffix = "|en-US|후속 언어|";
        var sourceValue = grammar == "array" ? (object)new[] { leaf.Jp,"second-language","第三言語" }
            : grammar == "delimited" ? leaf.Jp + suffix : leaf.Jp;
        var record = new Dictionary<string,object> { [physical] = sourceValue, ["ng04_unrelated_number"] = 77, ["ng04_unrelated_text"] = "unchanged" };
        foreach (var arrayRoot in new[] { false,true })
        {
            var input = arrayRoot ? JsonSerializer.Serialize(new[]{record}) : JsonSerializer.Serialize(record);
            var output = translator.Translate(leaf.Table,input,[leaf.Table]);
            using var parsed = JsonDocument.Parse(output);
            var actual = arrayRoot ? parsed.RootElement[0] : parsed.RootElement;
            var value = actual.GetProperty(physical);
            if (grammar == "array")
            {
                Require(value[0].GetString() == leaf.Ko,"first language not translated");
                Require(value[1].GetString() == "second-language" && value[2].GetString() == "第三言語", "other array languages altered");
            }
            else Require(value.GetString() == leaf.Ko + (grammar == "delimited" ? suffix : ""), "scalar/suffix preservation failed");
            Require(actual.GetProperty("ng04_unrelated_number").GetInt32() == 77 && actual.GetProperty("ng04_unrelated_text").GetString() == "unchanged","unrelated fields altered");
            Require(translator.Translate(leaf.Table,input,[]) == input,"disabled table parsed/mutated input");
        }
        Require(snapshot.Master.TryResolve(new(leaf.Table,leaf.Field,leaf.Jp),"field-grammar",out var catalog) && catalog.Ko == leaf.Ko, "grammar leaf view mismatch");
        return Task.CompletedTask;
    });
}
await Check("A04", "actual_translator_unmatched_non_string_and_empty_first_language_noop", () => {
    var leaf = leaves.First(p=>p.Field.EndsWith("[]"));
    var name = leaf.Field[..^2];
    foreach (var value in new object[] { Array.Empty<string>(), new[] { "unmatched-source", "second" }, 123, new[] {123,456} })
    {
        var input = JsonSerializer.Serialize(new Dictionary<string,object>{{name,value}});
        Require(translator.Translate(leaf.Table,input,[leaf.Table]) == input,"unsupported/unmatched value rewritten");
    }
    return Task.CompletedTask;
});
await Check("A03", "actual_translator_disabled_table_ref_and_raw_json_noop", () => {
    foreach (var resource in enabledRefs)
    {
        var r = resource.MasterRef!;
        Require(!translator.TryResolve(r.Table,r.Field,r.ExactJp,[],out _),"disabled table exposes translation");
        Require(!translator.ContainsOutput(r.Table,r.Field,master[r.Table][r.Field][r.ExactJp],[]),"disabled output capability exposed");
        const string deliberatelyUnparsed = "invalid JSON is untouched while table disabled";
        Require(translator.Translate(r.Table,deliberatelyUnparsed,[]) == deliberatelyUnparsed,"disabled path touched JSON");
    }
    return Task.CompletedTask;
});
await Check("A03", "actual_UiModuleHost_Translation_OFF_managed_restore_keeps_snapshot", () => {
    // Targets are CPU fixtures; calls exercise actual candidate ownership and OFF boundary.
    // Active native LegacyTranslationRuntime.Initialize is never invoked.
    var owner = new UiOwnershipService();
    var host = new UiModuleHost(snapshot, registry, owner);
    ulong id = 1000;
    var targets = new List<Target>();
    foreach (var resource in enabledRefs)
    {
        var binding = snapshot.Bindings.Values.First(b=>b.Enabled && b.ResourceId == resource.Id);
        var state = new TextTargetState(new(++id,(long)id,"CPU.fixture",1),resource.Jp,1,1,binding.SourceIdentity.IdentityJson,resource.Jp);
        var target = new Target(state); targets.Add(target);
        var ko = master[resource.MasterRef!.Table][resource.MasterRef.Field][resource.MasterRef.ExactJp];
        Require(owner.TryWrite(target,new("Consumers",binding.Id,null,binding.Selector.SlotKey,snapshot.Generations.Ui,state),ko).Written,"CPU fixture ownership setup failed");
    }
    var result = host.ReconcileUnavailableResources(false,true);
    Require(result.Committed && owner.LeaseCount == 0 && result.Restored == targets.Count,"OFF did not restore owned resources");
    Require(targets.All(t=>t.ReadState().Text == t.ReadState().ExactSource),"OFF left owned KO");
    Require(ReferenceEquals(host.Snapshot,snapshot),"OFF changed package authority");
    return Task.CompletedTask;
});
await Check("B09", "actual_candidate_accepted_Fit3_layout_policy", () => {
    var layout = snapshot.LayoutPolicies.Values.Single();
    Require(layout.BaseSize == 35 && layout.DescendingSizes.SequenceEqual(new[]{34,33,32,31}), "accepted layout policy changed");
    return Task.CompletedTask;
});

if (args.Length == 3)
{
    await Check("B01", "actual_candidate_entire_reviewed_generation_chain_and_legacy_session_identity", async () => {
        using var fixtures = JsonDocument.Parse(await File.ReadAllBytesAsync(args[2]));
        var fixture = fixtures.RootElement;
        Require(fixture.GetProperty("schema").GetString() is "gc-ng04-chain-fixtures-v1" or "gc-ng05-chain-fixtures-v1" && fixture.GetProperty("fixture_only").GetBoolean(), "chain fixture contract");
        var counting = new CountingSource();
        var chainLoader = new PackageLoader(counting,registry);
        var features = new Features();
        using var coordinator = new RuntimeCoordinator(chainLoader,registry,new UiOwnershipService(),new Scheduler(),features);
        var started = await coordinator.StartAsync(fixture.GetProperty("initial_root").GetString()!);
        Require(started.Committed && coordinator.Selected != null && features.Session != null,"actual coordinator initial chain selection failed");
        var initial = coordinator.Selected!;
        var legacy = features.Session!;
        var actualCatalog = await legacy.GetMasterAsync();
        var initialized = features.Initializations;
        var stageCount = 0;
        foreach (var stage in fixture.GetProperty("stages").EnumerateArray())
        {
            var stageRoot = stage.GetProperty("root").GetString()!;
            var prepared = await coordinator.PrepareAsync(stageRoot);
            Require(prepared != null,"chain generation preparation failed");
            var selected = coordinator.CommitUi(prepared!);
            Require(selected.Committed && coordinator.RestartRequired == null,"UI chain required legacy authority restart");
            var next = coordinator.Selected!;
            Require(new[]{"master","names","novel"}.All(component=>next.ComponentSignatures[component] == initial.ComponentSignatures[component]),"chain touched unrelated authority");
            Require(next.Generations.Master == initial.Generations.Master && next.Generations.Names == initial.Generations.Names && next.Generations.Novel == initial.Generations.Novel,"UI chain refreshed unrelated generations");
            Require(ReferenceEquals(legacy,features.Session) && features.Initializations == initialized,"actual session replaced during UI-only chain");
            Require(ReferenceEquals(await legacy.GetMasterAsync(),actualCatalog),"actual session Master task/cached translator replaced");
            var resource = next.Resources[stage.GetProperty("resource_id").GetString()!];
            Require(resource.Ko == stage.GetProperty("expected_ko").GetString(),"actual candidate stage KO lost");
            Require(stage.GetProperty("actual_io").GetProperty("novel_read_calls").GetInt32() == 0 && stage.GetProperty("actual_io").GetProperty("novel_hash_calls").GetInt32() == 0,"Python chain fixture read/hashed unchanged Novel");
            stageCount++;
        }
        var final = coordinator.Selected!;
        Require(final.Resources[fixture.GetProperty("resource_id").GetString()!].Ko == fixture.GetProperty("final_ko").GetString(),"actual final correction lost");
        var addedBindingId = fixture.GetProperty("binding_id").GetString()!;
        Require(!initial.Bindings.ContainsKey(addedBindingId),"added binding already existed in initial generation");
        Require(final.Bindings.TryGetValue(addedBindingId,out var addedBinding),"actual added binding missing");
        var routes = CompiledBattleRoute.ReadAll(typeof(PackageLoader).Assembly);
        var matchingRoutes = routes.Where(route=>route.Matches(addedBinding!)).ToArray();
        Require(matchingRoutes.Length == 1,"added binding does not identify exactly one compiled Battle route");
        var route = matchingRoutes[0];
        var expectedRoute = fixture.GetProperty("binding_route");
        Require(route.SourceId == expectedRoute.GetProperty("source_id").GetString()
            && route.Slot == expectedRoute.GetProperty("slot").GetString()
            && route.Owner == expectedRoute.GetProperty("owner").GetString()
            && route.Selector == expectedRoute.GetProperty("selector").GetString()
            && route.Callback == expectedRoute.GetProperty("callback").GetString()
            && route.Surface == expectedRoute.GetProperty("surface").GetString()
            && route.Segments.SequenceEqual(expectedRoute.GetProperty("segments").EnumerateArray().Select(x=>x.GetString()!),StringComparer.Ordinal),
            "reviewed route contract differs from actual DLL compiled definitions");
        Require(addedBinding!.Selector.Type == "caption" && addedBinding.Selector.Lifetime == "owner"
            && addedBinding.SourceIdentity.Kind == "current-model"
            && addedBinding.SourceIdentity.ExactJp == final.Resources[addedBinding.ResourceId].Jp,
            "added binding source/type/lifetime differs from existing Battle caption contract");
        Require(!initial.Bindings.Values.Any(route.Matches),"disposable initial generation already contains the added native route");
        Require(final.Bindings.Values.Count(route.Matches) == 1,"final generation has duplicate enabled bindings for compiled Battle route");
        compiledBattleRouteEvidence = new {
            axis="SOURCE_COMPILED_ROUTE", status="PASS", evidence="actual candidate DLL managed BattleRoutes.All reflection",
            binding_id=addedBinding.Id, source_id=route.SourceId, scope_key=addedBinding.Selector.ScopeKey,
            slot=route.Slot, owner=route.Owner, selector=route.Selector, callback=route.Callback,
            surface=route.Surface, segments=route.Segments, matching_compiled_routes=matchingRoutes.Length,
            matching_enabled_final_bindings=final.Bindings.Values.Count(route.Matches),
            native_hook_install="NOT_RUN", native_target_discovery="NOT_RUN"
        };
        var expectedLayout = fixture.GetProperty("layout");
        var policyId = fixture.GetProperty("layout_policy_id").GetString()!;
        var finalLayout = final.LayoutPolicies[policyId];
        // The fixture uses the existing typed schema. Canonical byte changes do
        // not authorize a new font grid or native method/property.
        Require(finalLayout.BaseSize == 35 && finalLayout.DescendingSizes.SequenceEqual(new[]{34,33,32,31}),"chain changed accepted native layout bounds");
        Require(finalLayout.BaseSize == expectedLayout.GetProperty("base_size").GetInt32()
            && finalLayout.FontProfile == expectedLayout.GetProperty("font_profile").GetString()
            && finalLayout.ScopeKey == expectedLayout.GetProperty("scope_key").GetString()
            && finalLayout.Restore == expectedLayout.GetProperty("restore").GetString()
            && finalLayout.OverflowFailure == expectedLayout.GetProperty("overflow_failure").GetString()
            && finalLayout.DescendingSizes.SequenceEqual(expectedLayout.GetProperty("descending_sizes").EnumerateArray().Select(x=>x.GetInt32())),"actual typed final layout differs from reviewed fixture");
        Require(counting.NovelReads == 0,"actual DLL chain loaded any unchanged Novel body");
        Require(stageCount == 4 && features.Selections == 4,"chain did not execute all four product transitions");
    });
}

var report = new {
    schema="gc-ng04-actual-candidate-master-CPU-v1", status=failures==0?"PASS":"FAIL", count=tests.Count, failures, tests,
    current_enabled_MasterRef_observation=enabledRefs.Length,
    assembly_sha256=PackageLoader.Hash(await File.ReadAllBytesAsync(typeof(PackageLoader).Assembly.Location)),
    assembly_informational_version=typeof(PackageLoader).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    observed_master_scope=new { tables=master.Count, fields=master.Values.Sum(x=>x.Count), entries=master.Values.Sum(x=>x.Values.Sum(y=>y.Count)) },
    cardinalities_are_permanent_gate=false,
    compiled_battle_route=compiledBattleRouteEvidence,
    game_interop_assembly_loaded=AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="Assembly-CSharp"),
    native_runtime_initialization="NOT_RUN; Config/Unity lifecycle guarded separately by source inspection",
    native_hook_install="NOT_RUN", game="NOT_RUN", install="NOT_PERFORMED"
};
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})+"\n");
Console.WriteLine($"ACTUAL NG04 MASTER CPU {(failures==0?"PASS":"FAIL")} {tests.Count-failures}/{tests.Count}");
return failures==0?0:1;

sealed class Target(TextTargetState initial):IUiTextTarget
{
    TextTargetState state=initial;
    public TextTargetState ReadState()=>state;
    public bool TryWriteText(TextTargetState expected,string value) { if (state!=expected)return false; state=state with {Text=value};return true; }
}
sealed record CompiledBattleRoute(string SourceId,string Slot,string Owner,string Selector,string Callback,string Surface,string[] Segments)
{
    public bool Matches(UiBinding binding) => binding.Enabled && binding.AdapterId == "battle.caption.v1"
        && binding.SourceIdentity.IdentityJson == JsonSerializer.Serialize(SourceId)
        && binding.Selector.ScopeKey == "battle-" + Surface.ToLowerInvariant()
        && binding.Selector.SlotKey == Slot && binding.Selector.Segments.SequenceEqual(Segments,StringComparer.Ordinal);
    public static CompiledBattleRoute[] ReadAll(Assembly candidate)
    {
        // The internal static data contains only strings and string arrays. Reading it
        // does not create a native provider, resolve a game type, or install a hook.
        var type = candidate.GetType("GCMod.Candidate.Native.BattleRoutes",throwOnError:true)!;
        var field = type.GetField("All",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("actual DLL compiled BattleRoutes.All missing");
        var values = field.GetValue(null) as Array ?? throw new InvalidOperationException("compiled Battle route data missing");
        return values.Cast<object>().Select(value=>{
            object Read(string name) => value.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(value)
                ?? throw new InvalidOperationException("compiled Battle route property missing: " + name);
            return new CompiledBattleRoute((string)Read("SourceId"),(string)Read("Slot"),(string)Read("Owner"),
                (string)Read("Selector"),(string)Read("Callback"),(string)Read("Surface"),(string[])Read("Segments"));
        }).ToArray();
    }
}
sealed class CountingSource:IPackageFileSource
{
    readonly LocalPackageFileSource actual = new();
    public int NovelReads;
    public async ValueTask<byte[]> ReadAsync(string root,string relativePath,CancellationToken cancellationToken=default)
    {
        if(relativePath.StartsWith("novels/",StringComparison.Ordinal))NovelReads++;
        return await actual.ReadAsync(root,relativePath,cancellationToken);
    }
}
sealed class Scheduler:IRuntimeMainThreadScheduler
{
    public bool IsMainThread=>true;
    public Task<T> InvokeAsync<T>(Func<T> callback,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(callback());}
}
sealed class Features:IRuntimeFeatureProvider
{
    public TranslationSession? Session;
    TranslationPackageSnapshot? active;
    public int Initializations,Selections;
    public IMasterTranslationView MasterView=>active?.Master??MasterTranslationView.Empty;
    public bool IsChoiceBusy=>false;
    public void Initialize(TranslationPackageSnapshot snapshot,UiModuleHost host){Initializations++;active=snapshot;Session=new TranslationSession(new LocalPackageTranslationStore(snapshot));}
    public void Selected(TranslationPackageSnapshot snapshot,UiModuleHost host){Selections++;active=snapshot;}
    public bool TryUninstall()=>true;
    public void Dispose()=>Session?.Dispose();
}
