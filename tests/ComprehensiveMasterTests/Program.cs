using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using GCMod.Candidate;
using GCMod.Nextgen;
using GCMod.Services;

if (args.Length != 3) throw new ArgumentException("package-root accepted-field-fixtures.json report.json");
var package = Path.GetFullPath(args[0]);
var corpus = JsonNode.Parse(await File.ReadAllBytesAsync(args[1]))!.AsArray();
var source = new CountingSource();
var snapshot = await new PackageLoader(source, AdapterRegistry.CreateNg01()).LoadAsync(package);
var local = new LocalPackageTranslationStore(snapshot);
var semantic = await local.FetchManifestAsync() ?? throw new InvalidOperationException("LOCAL_SEMANTIC_MANIFEST_REQUIRED");
var master = await local.LoadAsync<Dictionary<string, Dictionary<string, Dictionary<string,string>>>>(TranslationPaths.Master)
    ?? throw new InvalidOperationException("LOCAL_ACCEPTED_MASTER_REQUIRED");
using var session = new TranslationSession(local);
var translator = await session.GetMasterAsync() ?? throw new InvalidOperationException("ACTUAL_TRANSLATOR_REQUIRED");
var checks = new List<object>(); var failures = 0;
void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
void Check(string id, Action action)
{
    try { action(); checks.Add(new { id, status="PASS" }); }
    catch (Exception ex) { failures++; checks.Add(new { id, status="FAIL", reason=ex.Message, exception_type=ex.GetType().Name }); }
}
var profile = corpus.Select(x=>x!["table"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).ToArray();
foreach (var node in corpus)
{
    var fixture = node!;
    var table=fixture["table"]!.GetValue<string>(); var field=fixture["runtime_field"]!.GetValue<string>();
    var physical=fixture["field_name"]!.GetValue<string>(); var kind=fixture["kind"]!.GetValue<string>();
    var jp=fixture["exact_jp"]!.GetValue<string>(); var ko=fixture["accepted_ko"]!.GetValue<string>();
    Check("accepted-field:"+table+":"+field, () => {
        Require(master[table][field][jp] == ko,"FIXTURE_ACCEPTED_CATALOG_MISMATCH");
        Require(translator.TryResolve(table,field,jp,profile,out var resolved) && resolved==ko,"ACTUAL_SESSION_CATALOG_MISS");
        Require(!translator.TryResolve(table,field,jp,[],out _),"DISABLED_TABLE_RESOLVED");
        Require(!translator.TryResolve(table,field,jp+"__not_exact__",profile,out _),"NONEXACT_SOURCE_RESOLVED");
        foreach (var arrayRoot in new[]{false,true})
        {
            var record=fixture["input_record"]!.DeepClone().AsObject();
            record["__unchanged_number__"] = 123456; record["__unchanged_null__"] = null;
            if (kind=="delimited") record[physical] = jp+"| 123\r\n<color=#ffffff>suffix</color>|二番目";
            var input=(arrayRoot ? (JsonNode)new JsonArray(record) : record).ToJsonString();
            var output=translator.Translate(table,input,profile);
            var parsed=JsonNode.Parse(output)!; var actual=arrayRoot?parsed[0]!:parsed;
            var expected=fixture["expected_record"]!.DeepClone().AsObject();
            expected["__unchanged_number__"]=123456;expected["__unchanged_null__"]=null;
            if(kind=="delimited")expected[physical]=ko+"| 123\r\n<color=#ffffff>suffix</color>|二番目";
            Require(JsonNode.DeepEquals(actual,expected),"GRAMMAR_OR_UNRELATED_FIELD_CHANGED");
            Require(translator.Translate(table,input,[])==input,"DISABLED_TABLE_BYTES_CHANGED");
        }
        // No matching source, absent fields, unsupported scalars and empty arrays are exact no-ops.
        foreach(var invalidValue in new JsonNode?[]{null,JsonValue.Create(0),new JsonArray(),JsonValue.Create("__NO_ACCEPTED_SOURCE__")})
        {
            var record=new JsonObject { [physical]=invalidValue?.DeepClone(),["__unrelated__"]="KEEP" };
            var input=record.ToJsonString();
            Require(translator.Translate(table,input,profile)==input,"EMPTY_UNKNOWN_OR_NONSTRING_CHANGED");
        }
        Require(translator.Translate(table,"{\"unrelated\":77}",profile)=="{\"unrelated\":77}","MISSING_FIELD_CHANGED");
    });
}
foreach(var resource in snapshot.Resources.Values.Where(r=>r.Kind=="master_ref"))
{
    Check("master-ref:"+resource.Id,()=>{
        var r=resource.MasterRef!; var ko=master[r.Table][r.Field][r.ExactJp];
        Require(resource.Ko==null,"MASTER_REF_LITERAL_COPY");
        Require(snapshot.Master.TryResolve(r,resource.Role,out var value) && value.Ko==ko,"SNAPSHOT_MASTER_REF_MISS");
        Require(new UiResourceResolver(snapshot.Master).TryResolve(resource,out var resolved) && resolved==ko,"UI_MASTER_REF_RESOLVER_MISS");
        Require(translator.TryResolve(r.Table,r.Field,r.ExactJp,profile,out var active) && active==ko,"ACTUAL_SESSION_MASTER_REF_MISS");
        Require(!translator.TryResolve(r.Table,r.Field,r.ExactJp,[],out _),"DISABLED_MASTER_REF_RESOLVED");
    });
}
Check("unknown-and-disabled-table-no-parse",()=>{
    Require(translator.Translate("__unknown_table__","not json",profile)=="not json","UNKNOWN_TABLE_PARSED");
    Require(translator.Translate(profile[0],"not json",[])=="not json","DISABLED_TABLE_PARSED");
});
Check("empty-and-identical-KO-preserved",()=>{
    var custom=new MasterDataTranslator(new(){["table"]=new(){["text"]=new(){["jp"]="",["same"]="same"}}});
    foreach(var raw in new[]{"{\"text\":\"jp\"}","{\"text\":\"same\"}"}) Require(custom.Translate("table",raw,["table"])==raw,"EMPTY_OR_IDENTICAL_TRANSLATION_CHANGED");
});
Check("actual-session-cached-and-novel-body-not-read",()=>{
    Require(session.TryGetMaster(out var same) && ReferenceEquals(same,translator),"MASTER_SESSION_CACHE_REPLACED");
    Require(source.NovelReads==0,"UNRELATED_NOVEL_BODY_READ");
    Require(local.AuthorityIdentity==snapshot.ComponentSignatures["master"],"MASTER_AUTHORITY_MISMATCH");
});
var report=new { schema="gc-comprehensive-master-actual-candidate-CPU-v1", status=failures==0?"PASS":"FAIL", count=checks.Count, failures,
    assembly_sha256=PackageLoader.Hash(await File.ReadAllBytesAsync(typeof(MasterDataTranslator).Assembly.Location)),
    fixture_sha256=PackageLoader.Hash(await File.ReadAllBytesAsync(args[1])), fixtures_are_synthetic_records_from_accepted_entries=true,
    observed_scope=new{tables=master.Count,fields=master.Values.Sum(x=>x.Count),entries=master.Values.Sum(x=>x.Values.Sum(y=>y.Count)),grammar_fields=corpus.GroupBy(x=>x!["kind"]!.GetValue<string>()).ToDictionary(x=>x.Key,x=>x.Count())},
    application_novel_body_reads=source.NovelReads, actual_product_library=true, native_initialize="NOT_RUN", native_hook="NOT_RUN", game="NOT_RUN", checks };
await File.WriteAllTextAsync(args[2],JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true})+"\n");
Console.WriteLine($"COMPREHENSIVE MASTER CPU {(failures==0?"PASS":"FAIL")} {checks.Count-failures}/{checks.Count}");
return failures==0?0:1;

sealed class CountingSource:IPackageFileSource
{
    readonly LocalPackageFileSource inner=new(); public int NovelReads;
    public ValueTask<byte[]> ReadAsync(string root,string relativePath,CancellationToken cancellationToken=default)
    {
        if(relativePath.StartsWith("novels/",StringComparison.Ordinal)) NovelReads++;
        return inner.ReadAsync(root,relativePath,cancellationToken);
    }
}
