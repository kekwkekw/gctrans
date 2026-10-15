using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using GCMod.Nextgen;
using GCMod.Candidate;
using GCMod.Services;

if(args.Length!=4)throw new ArgumentException("chain.json UpdateCli.dll scratch-root report.json");
using var chain=JsonDocument.Parse(File.ReadAllBytes(args[0]));var fixture=chain.RootElement;
var initial=fixture.GetProperty("initial_root").GetString()!;
var final=fixture.GetProperty("stages").EnumerateArray().Last().GetProperty("root").GetString()!;
var area=Path.GetFullPath(args[2]);Directory.CreateDirectory(area);var storeRoot=Path.Combine(area,"isolated-runtime-store");
var registry=AdapterRegistry.CreateNg01();var loader=new PackageLoader(registry);var results=new List<object>();int failures=0;
void Require(bool condition,string code){if(!condition)throw new Exception(code);}
async Task Check(string name,Func<Task> run){try{await run();results.Add(new{name,status="PASS"});}catch(Exception error){failures++;results.Add(new{name,status="FAIL",reason=error.ToString()});}}
async Task<(int Code,JsonElement Report)> Prepare(string package,string root,string? expected=null)
{
    using var process=new Process{StartInfo=new ProcessStartInfo(Environment.ProcessPath!){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false}};
    foreach(var value in new[]{Path.GetFullPath(args[1]),"prepare-local","--store",root,"--package",package})process.StartInfo.ArgumentList.Add(value);
    if(expected!=null){process.StartInfo.ArgumentList.Add("--based-on");process.StartInfo.ArgumentList.Add(expected);}
    process.Start();var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();var output=await stdout;var err=await stderr;
    Require(output.Length>0,"CLI_NO_JSON:"+err);using var doc=JsonDocument.Parse(output);return(process.ExitCode,doc.RootElement.Clone());
}
string? active=null;
await Check("prepare-local saves complete pending without current/native initialization",async()=>{
    var (code,report)=await Prepare(initial,storeRoot);Require(code==0,"CLI_FAILED");Require(report.GetProperty("pending_saved").GetBoolean(),"FALSE_SAVE_SUCCESS");
    Require(!File.Exists(Path.Combine(storeRoot,"current.json"))&&!File.Exists(Path.Combine(storeRoot,"previous.json")),"PREPARE_SELECTED_CURRENT");
    Require(report.GetProperty("native_initialize").GetString()=="NOT_RUN","CLI_NATIVE_INITIALIZED");
    Require(await new LocalImmutablePackageStore(storeRoot,loader).RecoverPendingAsync()!=null,"PENDING_NOT_COMPLETE");
});
await Check("actual candidate CPU startup consumes local pending and actual legacy Master session",async()=>{
    var store=new LocalImmutablePackageStore(storeRoot,loader);var features=new Features();using var coordinator=new RuntimeCoordinator(loader,registry,new UiOwnershipService(),new Scheduler(),features,store);
    var result=await coordinator.StartAsync(Path.Combine(area,"invalid-bootstrap"));Require(result.Committed,"CPU_START_FAILED:"+result.Reason);active=coordinator.Selected!.Identity;
    Require(store.ReadPendingRestart()==null,"PENDING_NOT_FINISHED");Require(await features.Session!.GetMasterAsync()!=null,"REAL_LEGACY_SESSION_NO_MASTER");
    Require(features.Initialized==1,"DUPLICATE_CPU_INIT");
});
await Check("based-on conflict and invalid payload preserve current and previous",async()=>{
    var before=File.ReadAllBytes(Path.Combine(storeRoot,"current.json"));var (code,report)=await Prepare(final,storeRoot,"wrong");Require(code!=0&&report.GetProperty("reason").GetString()=="EXPECTED_CURRENT_CONFLICT","CAS_NOT_REJECTED");
    var bad=Path.Combine(area,"invalid");Directory.CreateDirectory(bad);var (badCode,badReport)=await Prepare(bad,storeRoot);Require(badCode!=0&&!badReport.GetProperty("pending_saved").GetBoolean(),"INVALID_PAYLOAD_ACCEPTED");
    Require(before.SequenceEqual(File.ReadAllBytes(Path.Combine(storeRoot,"current.json"))),"CURRENT_REPLACED_ON_FAILURE");
});
await Check("pending write failure is a failure and does not create current",async()=>{
    var root=Path.Combine(area,"fail-save");Directory.CreateDirectory(Path.Combine(root,"pending_restart.json"));var (code,report)=await Prepare(final,root);
    Require(code!=0&&!report.GetProperty("pending_saved").GetBoolean(),"SAVE_FAILURE_EXIT_SUCCESS");Require(!File.Exists(Path.Combine(root,"current.json")),"FAILURE_SELECTED_CURRENT");
});
await Check("UI next_package preparation preserves selection then restart selects complete same DLL package",async()=>{
    var before=File.ReadAllBytes(Path.Combine(storeRoot,"current.json"));var (code,report)=await Prepare(final,storeRoot,active);Require(code==0&&report.GetProperty("based_on").GetString()==active,"LOCAL_BASE_NOT_RECORDED");Require(before.SequenceEqual(File.ReadAllBytes(Path.Combine(storeRoot,"current.json"))),"CLI_SELECTED_NEW");
    var store=new LocalImmutablePackageStore(storeRoot,loader);var features=new Features();using var coordinator=new RuntimeCoordinator(loader,registry,new UiOwnershipService(),new Scheduler(),features,store);
    var start=await coordinator.StartAsync(initial);Require(start.Committed,"RESTART_PENDING_FAILED");Require(coordinator.Selected!.Identity==report.GetProperty("candidate").GetString(),"OLD_BOOTSTRAP_ROLLBACK");
    Require(coordinator.Selected.Resources[fixture.GetProperty("resource_id").GetString()!].Ko==fixture.GetProperty("final_ko").GetString(),"KO_FROM_PENDING_MISSING");
    Require(File.Exists(Path.Combine(storeRoot,"previous.json")),"PREVIOUS_LKG_LOST");
});
await Check("corrupt current falls back to valid previous LKG without broken bootstrap",async()=>{
    using var current=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(storeRoot,"current.json")));var id=current.RootElement.GetProperty("ContentId").GetString()!;
    var path=Path.Combine(storeRoot,"packages",id,"ui","manifest.json");var temporary=path+".test";File.WriteAllText(temporary,"{}");File.Move(temporary,path,true);
    var store=new LocalImmutablePackageStore(storeRoot,loader);var recovered=await store.RecoverAsync();Require(recovered!=null&&recovered.ContentId==active,"LKG_NOT_RECOVERED");
    var features=new Features();using var coordinator=new RuntimeCoordinator(loader,registry,new UiOwnershipService(),new Scheduler(),features,store);
    var start=await coordinator.StartAsync(Path.Combine(area,"invalid-bootstrap"));Require(start.Committed&&coordinator.Selected!.Identity==active,"CPU_LKG_START_FAILED");
});
var report=new{schema="gc-ng05-local-operations-CPU-v1",status=failures==0?"PASS":"FAIL",count=results.Count,failures,results,
    assembly_sha256=PackageLoader.Hash(File.ReadAllBytes(typeof(PackageLoader).Assembly.Location)),assembly_version=typeof(PackageLoader).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    actual_candidate_DLL=true,actual_local_store=true,real_managed_TranslationSession=true,scheduler="CPU_MOCK_MAIN_THREAD",native_initialize="NOT_RUN",native_hook="NOT_RUN",game="NOT_RUN",installation="NOT_PERFORMED"};
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);File.WriteAllText(args[3],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"NG05 LOCAL CPU {(failures==0?"PASS":"FAIL")} {results.Count-failures}/{results.Count}");return failures==0?0:1;

sealed class Scheduler:IRuntimeMainThreadScheduler {public bool IsMainThread=>true;public Task<T> InvokeAsync<T>(Func<T> callback,CancellationToken cancellationToken=default)=>Task.FromResult(callback());}
sealed class Features:IRuntimeFeatureProvider {
    public TranslationSession? Session;TranslationPackageSnapshot? snapshot;public int Initialized;
    public IMasterTranslationView MasterView=>snapshot?.Master??MasterTranslationView.Empty;public bool IsChoiceBusy=>false;
    public void Initialize(TranslationPackageSnapshot value,UiModuleHost host){Initialized++;snapshot=value;Session=new(new LocalPackageTranslationStore(value));}
    public void Selected(TranslationPackageSnapshot value,UiModuleHost host){snapshot=value;}
    public bool TryUninstall()=>true;public void Dispose()=>Session?.Dispose();
}
