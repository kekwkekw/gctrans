using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using GCMod.Nextgen;
using GCMod.Services;

if(args.Length!=2 || args[0] is not ("old" or "new"))throw new ArgumentException("old|new NEW_SCRATCH_ROOT");
string mode=args[0], root=Path.GetFullPath(args[1]);
if(Directory.Exists(root))throw new IOException("NEW_DISPOSABLE_SCRATCH_REQUIRED");
Directory.CreateDirectory(root);
var results=new List<object>();int failed=0;
void Need(bool condition,string reason){if(!condition)throw new InvalidDataException(reason);}
AdapterRegistry Registry(){var r=AdapterRegistry.CreateNg01();r.RegisterHostAdapter("core.static-caption.v1",SelectorContract.Host(["fixture-menu"],["sound"]));return r;}
async Task Check(string name,Func<Task> body){try{await body();results.Add(new{name,status="PASS"});}catch(Exception e){failed++;results.Add(new{name,status="FAIL",error=e.GetType().Name,reason=e.Message});}}
async Task<string> Fixture(string name,bool producer,string ko="사운드")
{
 string p=Path.Combine(root,name);Directory.CreateDirectory(Path.Combine(p,"ui"));Directory.CreateDirectory(Path.Combine(p,"layout"));
 string resources=JsonSerializer.Serialize(new{schema_version=1,locale="ko",resources=new[]{new{id="fixture.resource",role="sound",kind="literal",jp="サウンド",ko,disposition="translate"}}});
 string extra=producer?",\"producer\":{\"family\":\"core-literal-v1\",\"localizer_type\":0,\"text_config_key\":\"fixture.metadata\"}":"";
 string bindings="{\"schema_version\":1,\"bindings\":[{\"id\":\"fixture.binding\",\"resource_id\":\"fixture.resource\",\"adapter_id\":\"core.static-caption.v1\",\"enabled\":true,\"source_identity\":{\"kind\":\"static-asset\",\"identity\":\"fixture:source\",\"exact_jp\":\"サウンド\"},\"selector\":{\"scope_key\":\"fixture-menu\",\"slot_key\":\"sound\",\"type\":\"static\"}"+extra+"}]}";
 string master="{\"mFixture\":{\"name\":{\"原文\":\"원문\"}}}", names="{\"人\":\"사람\"}";
 var semantic=new{names=TranslationHash.Compute(names),master=TranslationHash.Compute(master),novels=new Dictionary<string,string>()};
 string legacy=JsonSerializer.Serialize(new{hash=TranslationHash.Compute(JsonSerializer.Serialize(semantic)),semantic.names,semantic.master,semantic.novels});
 var rows=new Dictionary<string,(string Kind,string Text)>{["ui/resources.json"]=("resources",resources),["ui/bindings.json"]=("bindings",bindings),["master.json"]=("master",master),["layout/episode-title.json"]=("layout","{\"schema_version\":1,\"policy_id\":\"episode-title.fit.v1\",\"font_profile\":\"RHR_Medium_GFSAFE_ADH\",\"base_size\":35,\"descending_sizes\":[34,33,32,31],\"restore\":\"owned-only\",\"overflow_failure\":\"restore-base\",\"scope_key\":\"episode-list-title\"}")};
 var outside=new Dictionary<string,(string Kind,string Text)>{["names.json"]=("names",names),["manifest.json"]=("legacy_manifest",legacy)};
 object Descriptor(string key,(string Kind,string Text) value){var bytes=Encoding.UTF8.GetBytes(value.Text);return new{path=key,kind=value.Kind,sha256=PackageLoader.Hash(bytes),size=bytes.Length};}
 foreach(var x in rows.Concat(outside))await File.WriteAllTextAsync(Path.Combine(p,x.Key),x.Value.Text,new UTF8Encoding(false));
 string index=JsonSerializer.Serialize(new{schema_version=1,files=outside.Select(x=>Descriptor(x.Key,x.Value)).ToArray()});
 await File.WriteAllTextAsync(Path.Combine(p,"ui/legacy-components.json"),index,new UTF8Encoding(false));rows["ui/legacy-components.json"]=("legacy_index",index);
 await File.WriteAllTextAsync(Path.Combine(p,"ui/manifest.json"),JsonSerializer.Serialize(new{schema_version=1,locale="ko",origin="disposable-transition-fixture",revision=name,files=rows.Select(x=>Descriptor(x.Key,x.Value)).ToArray()}),new UTF8Encoding(false));return p;
}
RuntimeCoordinator Coordinator(PackageLoader l,AdapterRegistry r,LocalImmutablePackageStore s)=>new(l,r,new UiOwnershipService(),new ManagedScheduler(),new ManagedFeatures(),s);
string Pointer(string store)=>File.ReadAllText(Path.Combine(store,"current.json"));
string oldRoot=await Fixture("old-shape",false),newRoot=await Fixture("new-shape",true),otherRoot=await Fixture("other-shape",false,"다른 소리");
if(mode=="old")
{
 await Check("old_client_old_shape_actual_loader_and_nonempty_store",async()=>{
  var registry=Registry();var l=new PackageLoader(registry);var s=new LocalImmutablePackageStore(Path.Combine(root,"old-baseline-store"),l);var old=await s.StageAsync(oldRoot);s.Select(old.Snapshot);
  var recovered=await s.RecoverAsync();Need(recovered?.ContentId==old.ContentId,"old baseline not recovered");
 });
 await Check("old_client_new_producer_expected_reject_and_current_LKG_preserved",async()=>{
  var registry=Registry();var l=new PackageLoader(registry);string store=Path.Combine(root,"old-reject-store");var s=new LocalImmutablePackageStore(store,l);var old=await s.StageAsync(oldRoot);s.Select(old.Snapshot);string before=Pointer(store);bool rejected=false;
  try{await s.StageAsync(newRoot);}catch(PackageValidationException e){rejected=e.Message.Contains("UNKNOWN_PROPERTY",StringComparison.Ordinal)&&e.Message.Contains("producer",StringComparison.Ordinal);}
  Need(rejected,"new producer not explicitly rejected by old loader");Need(Pointer(store)==before,"current changed on rejection");Need((await s.RecoverAsync())?.ContentId==old.ContentId,"LKG lost after rejection");
  using var c=Coordinator(l,registry,s);var start=await c.StartAsync(newRoot);Need(start.Committed&&c.Selected?.Identity==old.ContentId,"old LKG start failed");Need((c.Selected??throw new InvalidDataException("missing selection")).Identity!=PackageLoader.Hash(await File.ReadAllBytesAsync(Path.Combine(newRoot,"ui/manifest.json"))),"incorrect restoration completion");
 });
}
else
{
 await Check("new_client_accepts_new_producer_actual_loader",async()=>{var v=await new PackageLoader(Registry()).LoadAsync(newRoot);Need(v.Bindings.Values.Single().GetType().GetProperty("Producer")?.GetValue(v.Bindings.Values.Single())!=null,"producer discarded");});
 await Check("nonempty_old_current_prepare_pending_start_and_restart_preserve_new_selection",async()=>{
  var r=Registry();var l=new PackageLoader(r);string path=Path.Combine(root,"transition-store");var s=new LocalImmutablePackageStore(path,l);var old=await s.StageAsync(oldRoot);s.Select(old.Snapshot);string before=Pointer(path),next="";
  using(var c=Coordinator(l,r,s)){var b=await c.RecoverPreparationBaselineAsync();Need(b?.Identity==old.ContentId,"baseline missing");var p=await c.PrepareAsync(newRoot);Need(p!=null&&c.SavePendingStartup(p,old.ContentId),"pending not saved");next=p!.Snapshot.Identity;Need(Pointer(path)==before,"prepare selected early");var started=await c.StartAsync(oldRoot);Need(started.Committed&&c.Selected?.Identity==next&&s.ReadPendingRestart()==null,"pending startup did not commit");}
  using(var c=Coordinator(new PackageLoader(r),r,new LocalImmutablePackageStore(path,new PackageLoader(r)))){var started=await c.StartAsync(oldRoot);Need(started.Committed&&c.Selected?.Identity==next,"old bootstrap overrode current on restart");}
 });
 await Check("pending_expected_base_conflict_keeps_actual_current",async()=>{
  var r=Registry();var l=new PackageLoader(r);string path=Path.Combine(root,"conflict-store");var s=new LocalImmutablePackageStore(path,l);var old=await s.StageAsync(oldRoot);var n=await s.StageAsync(newRoot);var other=await s.StageAsync(otherRoot);s.Select(old.Snapshot);s.RecordPendingRestart(n.Snapshot,old.ContentId,"fixture");s.Select(other.Snapshot);
  using var c=Coordinator(l,r,s);var started=await c.StartAsync(newRoot);Need(started.Committed&&c.Selected?.Identity==other.ContentId&&s.ReadPendingRestart()==null,"stale pending replaced independent current");
 });
 await Check("invalid_pending_package_falls_back_without_restoration_completion",async()=>{
  var r=Registry();var l=new PackageLoader(r);string path=Path.Combine(root,"invalid-pending-store");var s=new LocalImmutablePackageStore(path,l);var old=await s.StageAsync(oldRoot);var n=await s.StageAsync(newRoot);s.Select(old.Snapshot);s.RecordPendingRestart(n.Snapshot,old.ContentId,"fixture");string leaf=Path.Combine(n.Root,"ui/bindings.json");File.Move(leaf,leaf+".fixture-bad-original");await File.WriteAllTextAsync(leaf,"{}");
  using var c=Coordinator(l,r,s);var started=await c.StartAsync(newRoot);Need(started.Committed&&c.Selected?.Identity==old.ContentId&&c.Selected.Identity!=n.ContentId&&s.ReadPendingRestart()==null,"invalid new package was selected");
 });
}
Console.WriteLine(JsonSerializer.Serialize(new{status=failed==0?"PASS":"FAIL",mode,count=results.Count,failed,actual_library_SHA256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(PackageLoader).Assembly.Location))).ToLowerInvariant(),scope="ACTUAL_PRODUCT_LOADER_STORE_COORDINATOR; DISPOSABLE_SYNTHETIC_SMALL_PACKAGE; MANAGED_FEATURE_CALLBACK_MOCK; NOT_NATIVE_PROOF",results},new JsonSerializerOptions{WriteIndented=true}));return failed==0?0:1;
sealed class ManagedScheduler:IRuntimeMainThreadScheduler{public bool IsMainThread=>true;public Task<T> InvokeAsync<T>(Func<T> cb,CancellationToken ct=default){ct.ThrowIfCancellationRequested();return Task.FromResult(cb());}}
sealed class ManagedFeatures:IRuntimeFeatureProvider{public IMasterTranslationView MasterView=>MasterTranslationView.Empty;public bool IsChoiceBusy=>false;public void Initialize(TranslationPackageSnapshot s,UiModuleHost h){}public void Selected(TranslationPackageSnapshot s,UiModuleHost h){}public bool TryUninstall()=>true;public void Dispose(){}}
