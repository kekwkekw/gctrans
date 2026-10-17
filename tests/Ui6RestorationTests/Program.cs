using System.Collections.Immutable;
using System.Text.Json;
using GCMod.Nextgen;

if (args.Length != 2) throw new ArgumentException("bindings.json report.json");
using var doc = JsonDocument.Parse(await File.ReadAllBytesAsync(args[0]));
var rows = doc.RootElement.GetProperty("bindings").EnumerateArray().ToArray();
var cases = new[]{
    ("UI6-FILTER-CheckBox","Gc.CheckBoxFilterGroup",(int?)null),
    ("UI6-FILTER-Toggle","Gc.ToggleFilterGroup",(int?)null),
    ("UI6-SORT-UI4-M015","Gc.SortPopup",(int?)0),
    ("UI6-SORT-UI4-M019","Gc.SortPopup",(int?)10),
    ("UI6-UI4-M006-OWNED_CATEGORY_FRAME_TEXT","Gc.Shop.ShopCategoryScrollCell",(int?)null),
    ("UI6-UI4-M006-OWNED_CATEGORY_SECTION_TITLE","Gc.Shop.ShopCategoryScrollCell",(int?)null)
};
var results = new List<object>(); int failures=0;
void Check(string id,Action check) { try{check();results.Add(new{id,status="PASS"});}catch(Exception e){failures++;results.Add(new{id,status="FAIL",reason=e.Message});} }
void Require(bool yes,string reason){if(!yes)throw new InvalidOperationException(reason);}
UiBinding Read(string identity)
{
    var raw=rows.Single(r=>r.GetProperty("source_identity").GetProperty("identity").ValueKind==JsonValueKind.String&&r.GetProperty("source_identity").GetProperty("identity").GetString()==identity);
    var selector=raw.GetProperty("selector");var source=raw.GetProperty("source_identity");
    return new(raw.GetProperty("id").GetString()!,raw.GetProperty("resource_id").GetString()!,raw.GetProperty("adapter_id").GetString()!,raw.GetProperty("enabled").GetBoolean(),
        new(source.GetProperty("kind").GetString()!,source.GetProperty("identity").GetRawText(),source.GetProperty("exact_jp").GetString()!),
        new(selector.GetProperty("scope_key").GetString()!,selector.GetProperty("slot_key").GetString()!,selector.TryGetProperty("segments",out var paths)?paths.EnumerateArray().Select(x=>x.GetString()!).ToImmutableArray():[],Type:selector.GetProperty("type").GetString(),Lifetime:selector.GetProperty("lifetime").GetString()),null);
}
foreach(var (identity,type,value) in cases)
{
    var binding=Read(identity);
    Check(identity+":accepted-current-managed-route",()=>{
        Require(Ui6ConsumerRoutePolicy.Matches(binding,identity,type,"Setup",value),"ACCEPTED_MANAGED_CONTRACT_NOT_CONNECTED");
        Require(Ui6ConsumerRoutePolicy.UsesScopedConsumerRenderer(binding),"ACCEPTED_MODEL_RENDERER_NOT_SELECTED");
    });
    Check(identity+":negative-boundaries",()=>{
        Require(!Ui6ConsumerRoutePolicy.Matches(binding with{Enabled=false},identity,type,"Setup",value),"DISABLED_ROUTE_ENABLED");
        Require(!Ui6ConsumerRoutePolicy.Matches(binding,identity,"Other.Type","Setup",value),"WRONG_CURRENT_CONSUMER_ACCEPTED");
        Require(!Ui6ConsumerRoutePolicy.Matches(binding,identity,type,"OtherMethod",value),"WRONG_CALLBACK_ACCEPTED");
        Require(!Ui6ConsumerRoutePolicy.Matches(binding with{Selector=binding.Selector with{ScopeKey="other"}},identity,type,"Setup",value),"WRONG_SCOPE_ACCEPTED");
        Require(!Ui6ConsumerRoutePolicy.UsesScopedConsumerRenderer(binding with{Selector=binding.Selector with{ScopeKey="other"}}),"UNSUPPORTED_RENDERER_SCOPE_SELECTED");
        Require(!Ui6ConsumerRoutePolicy.Matches(binding with{Selector=binding.Selector with{Lifetime="other"}},identity,type,"Setup",value),"WRONG_LIFETIME_ACCEPTED");
        Require(!Ui6ConsumerRoutePolicy.Matches(binding with{SourceIdentity=binding.SourceIdentity with{IdentityJson="\"other\""}},identity,type,"Setup",value),"WRONG_SOURCE_IDENTITY_ACCEPTED");
        Require(!Ui6ConsumerRoutePolicy.UsesScopedConsumerRenderer(binding with{SourceIdentity=binding.SourceIdentity with{IdentityJson="\"other\""}}),"UNKNOWN_CONSUMER_RENDERER_SELECTED");
        Require(!Ui6ConsumerRoutePolicy.Matches(binding,identity,type,"Setup",value==null?0:null),"WRONG_CURRENT_VALUE_ACCEPTED");
    });
}
Check("checkbox-does-not-use-toggle-role",()=>Require(!Ui6ConsumerRoutePolicy.Matches(Read("UI6-FILTER-CheckBox"),"UI6-FILTER-CheckBox","Gc.ToggleFilterGroup","Setup"),"CROSS_CONSUMER_IDENTITY_ACCEPTED"));
Check("rarity-and-acquisition-values-are-distinct",()=>{
    Require(!Ui6ConsumerRoutePolicy.Matches(Read("UI6-SORT-UI4-M015"),"UI6-SORT-UI4-M015","Gc.SortPopup","Setup",10),"ACQUISITION_USED_AS_RARITY");
    Require(!Ui6ConsumerRoutePolicy.Matches(Read("UI6-SORT-UI4-M019"),"UI6-SORT-UI4-M019","Gc.SortPopup","Setup",0),"RARITY_USED_AS_ACQUISITION");
});
Check("unaccepted-attribute-and-set-routes-stay-disabled",()=>{
    Require(Ui6ConsumerRoutePolicy.Find("UI6-SORT-UI4-M018")==null,"UNACCEPTED_ATTRIBUTE_SCOPE_ADDED");
    foreach(var identity in new[]{"UI6-SORT-UI4-M018","UI6-UI4-M007-OWNED_CATEGORY_FRAME_TEXT","UI6-UI4-M007-OWNED_CATEGORY_SECTION_TITLE"})Require(!Read(identity).Enabled,"UNACCEPTED_ARCHIVE_BINDING_ENABLED");
});
var report=new{schema="gc-ui6-restoration-actual-candidate-managed-route-cpu-v1",status=failures==0?"PASS":"FAIL",count=results.Count,failures,
    assembly_sha256=PackageLoader.Hash(await File.ReadAllBytesAsync(typeof(Ui6ConsumerRoutePolicy).Assembly.Location)),
    evidence_axis="ACTUAL_PRODUCT_MANAGED_ROUTE_POLICY_ONLY; no native bool fixtures",native_callback_model_target="NOT_RUN",native_display="NOT_RUN",results};
await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})+"\n");
Console.WriteLine($"UI6 RESTORATION CPU {(failures==0?"PASS":"FAIL")} {results.Count-failures}/{results.Count}");return failures==0?0:1;
