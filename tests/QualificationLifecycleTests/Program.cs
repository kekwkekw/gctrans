using System.Reflection;using System.Text.Json;using GCMod.Nextgen;using GCMod.Services;using GCMod.Candidate;
if(args.Length!=4)throw new ArgumentException("initial-package changed-next-package restored-next-package report.json");
var source=new CountedSource();var features=new LegacyFeatures();var registry=AdapterRegistry.CreateNg01();
var policy=typeof(RuntimeCoordinator).Assembly.GetType("GCMod.Candidate.Native.CompiledSelectorPolicy",true)!.GetMethod("For",BindingFlags.Public|BindingFlags.Static)!;
foreach(var (id,module) in new[]{("core.static-caption.v1","Core"),("ui4.static-caption.v1","Core"),("core.current-model.v1","Core"),("core.dynamic-caption.v1","Core"),("common.model-label.v1","Consumers"),("common.date-caption.v1","Consumers"),("ui.chrome.v1","Chrome"),("ui.residual-caption.v1","Residuals"),("ui.caption.v1","Captions")})registry.RegisterHostAdapter(id,(SelectorContract)policy.Invoke(null,new object[]{id})!,module);
var coordinator=new RuntimeCoordinator(new PackageLoader(source,registry),registry,new UiOwnershipService(),new CpuScheduler(),features);
var results=new List<object>();int failures=0;
void Need(bool b,string reason){if(!b)throw new InvalidDataException(reason);}
async Task T(string name,Func<Task> action){try{await action();results.Add(new{name,status="PASS"});}catch(Exception e){failures++;results.Add(new{name,status="FAIL",reason=e.Message});}}
TranslationPackageSnapshot? original=null;TranslationSession? session=null;Task<Dictionary<string,string>?>? namesTask=null;
// These are actual portable coordinator/store/session paths in the delivered DLL.
// The feature bridge and scheduler are CPU stubs; they do not claim native Initialize/display proof.
await T("actual_candidate_complete_package_start_and_existing_session",async()=>{Need((await coordinator.StartAsync(args[0])).Committed,"startup_failed");original=coordinator.Selected;session=features.Session;namesTask=session!.GetNamesAsync();Need(await namesTask!=null,"names_missing");});
if(original!=null&&session!=null){
 foreach(var pair in new[]{(args[1],"discarded_KO_correction"),(args[2],"original_KO_restoration")})await T(pair.Item2+"_UI_commit_keeps_legacy_cache_and_zero_novel_reads",async()=>{
  var before=coordinator.Selected;source.NovelReads=0;var prepared=await coordinator.PrepareAsync(pair.Item1);Need(prepared!=null&&ReferenceEquals(coordinator.Selected,before),"prepare_selected_early");Need(coordinator.CommitUi(prepared!).Committed,"ui_commit_failed");Need(ReferenceEquals(session,features.Session)&&ReferenceEquals(namesTask,features.Session!.GetNamesAsync()),"legacy_session_or_names_cache_refreshed");Need(source.NovelReads==0,"unchanged_novel_read");
 });
 await T("original_resource_semantics_restored_and_clean_unload",()=>{var final=coordinator.Selected??throw new InvalidDataException("selection_missing");Need(original.Resources.All(x=>final.Resources[x.Key].Ko==x.Value.Ko),"KO_not_restored");Need(coordinator.TryUnload(),"unload_failed");return Task.CompletedTask;});
}
var report=new{status=failures==0?"PASS":"FAIL",failed=failures,count=results.Count,actual_library_sha256=PackageLoader.Hash(File.ReadAllBytes(typeof(RuntimeCoordinator).Assembly.Location)),scope="ACTUAL_CANDIDATE_PORTABLE_COORDINATOR_LOCAL_STORE_LEGACY_SESSION; CPU feature/scheduler stubs; native/display NOT_CLAIMED; isolated discarded packages only",results};File.WriteAllText(args[3],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report));return failures==0?0:1;
sealed class CountedSource:IPackageFileSource{readonly LocalPackageFileSource source=new();public int NovelReads;public async ValueTask<byte[]> ReadAsync(string root,string path,CancellationToken cancel=default){if(path.StartsWith("novels/",StringComparison.Ordinal))NovelReads++;return await source.ReadAsync(root,path,cancel);}}
sealed class CpuScheduler:IRuntimeMainThreadScheduler{public bool IsMainThread=>true;public Task<T> InvokeAsync<T>(Func<T> callback,CancellationToken cancel=default){cancel.ThrowIfCancellationRequested();return Task.FromResult(callback());}}
sealed class LegacyFeatures:IRuntimeFeatureProvider{public TranslationSession? Session;public IMasterTranslationView MasterView=>MasterTranslationView.Empty;public bool IsChoiceBusy=>false;public void Initialize(TranslationPackageSnapshot package,UiModuleHost host){Session=new(new LocalPackageTranslationStore(package));}public void Selected(TranslationPackageSnapshot p,UiModuleHost h){}public bool TryUninstall()=>true;public void Dispose()=>Session?.Dispose();}
