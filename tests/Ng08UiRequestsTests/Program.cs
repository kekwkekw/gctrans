using GCMod.Nextgen;
using GCMod.Candidate;
using GCMod.Services;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;

if(args.Length!=4)throw new ArgumentException("fixture.json UpdateCli.dll scratch report.json");
using var f=JsonDocument.Parse(File.ReadAllBytes(args[0]));var fixture=f.RootElement;
string initial=fixture.GetProperty("initial_root").GetString()!,changed=fixture.GetProperty("changed").GetString()!;
var scratch=Path.GetFullPath(args[2]);Directory.CreateDirectory(scratch);var root=Path.Combine(scratch,"runtime-store");
var registry=AdapterRegistry.CreateNg01();var loader=new PackageLoader(registry);var scheduler=new Scheduler();var features=new Features();
var store=new LocalImmutablePackageStore(root,loader);using var coordinator=new RuntimeCoordinator(loader,registry,new UiOwnershipService(),scheduler,features,store);
var channel=new LocalUiRequestChannel(root);using var driver=new LocalUiRequestDriver(coordinator,scheduler,channel,new SafeDiagnostics());
var results=new List<object>();int failed=0;long frameTimestamp=0;
void Need(bool value,string reason){if(!value)throw new Exception(reason);}
async Task Check(string name,Func<Task> action){try{await action();results.Add(new{name,status="PASS"});}catch(Exception e){failed++;results.Add(new{name,status="FAIL",reason=e.ToString()});}}
void Frame(bool enabled=true,bool otherBusy=false){scheduler.Frame(()=>driver.Tick(enabled,otherBusy,frameTimestamp));frameTimestamp+=Stopwatch.Frequency;}
async Task Pump(Task task){var timer=Stopwatch.StartNew();while(!task.IsCompleted){scheduler.Pump();Need(timer.Elapsed<TimeSpan.FromSeconds(120),"SCHEDULER_TIMEOUT");await Task.Delay(1);}scheduler.Pump();await task;}
async Task Until(Func<bool> done){var timer=Stopwatch.StartNew();while(!done()){scheduler.Pump();Frame();Need(timer.Elapsed<TimeSpan.FromSeconds(120),"DRIVER_TIMEOUT");await Task.Delay(2);}await Pump(driver.Worker);}
async Task<JsonElement> Cli(string package,string basedOn,string? id=null){var info=new ProcessStartInfo("C:/Program Files/dotnet/dotnet.exe"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};foreach(var a in new[]{Path.GetFullPath(args[1]),"request-ui","--store",root,"--package",package,"--based-on",basedOn})info.ArgumentList.Add(a);if(id!=null){info.ArgumentList.Add("--request-id");info.ArgumentList.Add(id);}using var p=Process.Start(info)!;var output=p.StandardOutput.ReadToEndAsync();var errors=p.StandardError.ReadToEndAsync();await p.WaitForExitAsync();var text=await output;Need(text.Length>0,"CLI_NO_REPORT:"+await errors);using var doc=JsonDocument.Parse(text);return doc.RootElement.Clone();}
async Task PumpOn(Scheduler queued,Task task){var timer=Stopwatch.StartNew();while(!task.IsCompleted){queued.Pump();Need(timer.Elapsed<TimeSpan.FromSeconds(120),"RACE_SCHEDULER_TIMEOUT");await Task.Delay(1);}queued.Pump();await task;}
async Task WaitQueued(Scheduler queued){var timer=Stopwatch.StartNew();while(queued.Pending==0){Need(timer.Elapsed<TimeSpan.FromSeconds(120),"RACE_MAIN_CALLBACK_TIMEOUT");await Task.Delay(1);}}
async Task PreparedPublicationRace(bool cancel,bool replaceClaim=false)
{
 // Fresh owned CPU store, real immutable package/channel, and the existing queued
 // main-thread scheduler. The OS lock blocks the actual PREPARED report; there is
 // no product hook or fabricated native availability in this test.
 var caseRoot=Path.Combine(scratch,(replaceClaim?"prepared-report-foreign-":cancel?"prepared-report-cancel-":"prepared-report-commit-")+Guid.NewGuid().ToString("N"));
 var queued=new Scheduler();var ownedFeatures=new Features();var ownedStore=new LocalImmutablePackageStore(caseRoot,loader);
 using var runtime=new RuntimeCoordinator(loader,registry,new UiOwnershipService(),queued,ownedFeatures,ownedStore);
 var transport=new LocalUiRequestChannel(caseRoot);using var ownedDriver=new LocalUiRequestDriver(runtime,queued,transport,new SafeDiagnostics());
 var startup=runtime.StartAsync(initial);await PumpOn(queued,startup);Need(startup.Result.Committed,"RACE_START_FAILED");
 string baseline=runtime.Selected!.Identity;var staged=await ownedStore.StageAsync(changed,runtime.Selected);
 Need(staged.ContentId!=baseline,"RACE_CHANGED_PACKAGE_REQUIRED");
 File.WriteAllText(Path.Combine(caseRoot,"pending_restart.json"),JsonSerializer.Serialize(new PendingRestartSelection(1,baseline,baseline,"RACE_CPU_SENTINEL")));
 var currentBytes=File.ReadAllBytes(Path.Combine(caseRoot,"current.json"));var pendingBytes=File.ReadAllBytes(Path.Combine(caseRoot,"pending_restart.json"));
 var requestId=Guid.NewGuid().ToString("N");Need(transport.Publish(new(1,requestId,staged.ContentId,baseline,"ui_reload"))=="UI_REQUEST_PUBLISHED","RACE_PUBLISH_FAILED");
 var foreignBytes=JsonSerializer.SerializeToUtf8Bytes(new LocalUiRequest(1,Guid.NewGuid().ToString("N"),staged.ContentId,baseline,"ui_reload"));
 queued.Frame(()=>ownedDriver.Tick(true,false,0));var pollWorker=ownedDriver.Worker;
 await WaitQueued(queued);Need(queued.PumpOne(),"RACE_CLAIM_CALLBACK_MISSING");
 Need(File.Exists(Path.Combine(caseRoot,"ui-request.claim.json")),"RACE_ACTUAL_CLAIM_REQUIRED");
 // PrepareStoredUiAsync has no main-thread callback. The next queued callback is
 // the driver's PREPARED publication. Lock after claim, before publishing it.
 await WaitQueued(queued);
 var reportLock=new FileStream(Path.Combine(caseRoot,".ui-request.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);
 bool noEarlyCommit=false,noEarlyCurrentWrite=false,workerBlocked=false;Task tickWorker=Task.CompletedTask,cancelWorker=Task.CompletedTask;
 try
 {
  Need(queued.PumpOne(),"RACE_PREPARED_CALLBACK_MISSING");Need(ownedDriver.LastOutcome?.Status=="PREPARED","RACE_WRONG_PUBLICATION_BOUNDARY");
  workerBlocked=!pollWorker.IsCompleted;
  // With the old prepared-before-polling Tick order, this exact frame called
  // CommitUiRequest even though PREPARED could not be written under this lock.
  queued.Frame(()=>ownedDriver.Tick(true,false,Stopwatch.Frequency));tickWorker=ownedDriver.Worker;
  noEarlyCommit=ownedFeatures.SelectedCalls==0&&runtime.Selected!.Identity==baseline;
  noEarlyCurrentWrite=currentBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(caseRoot,"current.json")));
  // A non-cooperating external writer replaces the exact claimed bytes while the
  // real report is locked out. It remains valid data, but belongs to another ID.
  if(replaceClaim)File.WriteAllBytes(Path.Combine(caseRoot,"ui-request.claim.json"),foreignBytes);
  if(cancel){ownedDriver.Dispose();cancelWorker=ownedDriver.Worker;}
 }
 finally{reportLock.Dispose();}
 await PumpOn(queued,Task.WhenAll(pollWorker,tickWorker,cancelWorker));
 Need(workerBlocked,"RACE_WORKER_NOT_BLOCKED_AT_PREPARED_REPORT");Need(noEarlyCommit,"COMMIT_BEFORE_PREPARED_REPORT_FINISHED");Need(noEarlyCurrentWrite,"CURRENT_WRITTEN_BEFORE_PREPARED_REPORT_FINISHED");
 Need(pendingBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(caseRoot,"pending_restart.json"))),"RACE_PENDING_CHANGED");
 if(replaceClaim)
 {
  Need(runtime.Selected!.Identity==baseline&&ownedFeatures.SelectedCalls==0,"FOREIGN_CLAIM_SELECTED_CANDIDATE");
  Need(currentBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(caseRoot,"current.json"))),"FOREIGN_CLAIM_CURRENT_CHANGED");
  var outcome=ownedDriver.LastOutcome;Need(outcome?.RequestId==requestId&&outcome.Status=="REJECTED"&&outcome.Reason=="UI_REQUEST_CLAIM_CHANGED"&&outcome.CurrentContentId==baseline&&!outcome.Persisted,"FOREIGN_CLAIM_MANAGED_OUTCOME_MISMATCH");
  Need(!ownedDriver.IsBusy,"FOREIGN_CLAIM_PREPARATION_RETAINED");Need(transport.LastResult()==null,"FOREIGN_CLAIM_WRONG_RESULT_WRITTEN");
  queued.Frame(()=>ownedDriver.Tick(false,false,2*Stopwatch.Frequency));ownedDriver.Dispose();await PumpOn(queued,ownedDriver.Worker);
  Need(ownedFeatures.SelectedCalls==0&&runtime.Selected!.Identity==baseline,"FOREIGN_CLAIM_LATE_COMMIT");
  Need(foreignBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(caseRoot,"ui-request.claim.json"))),"FOREIGN_CLAIM_BYTES_CHANGED_OR_DELETED");
 }
 else if(cancel)
 {
  Need(runtime.Selected!.Identity==baseline&&ownedFeatures.SelectedCalls==0,"CANCELLED_REPORT_SELECTED_CANDIDATE");
  Need(currentBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(caseRoot,"current.json"))),"CANCELLED_REPORT_CURRENT_CHANGED");
  var terminal=transport.LastResult();Need(terminal?.RequestId==requestId&&terminal.Status=="REJECTED"&&terminal.Reason=="UI_REQUEST_LIFETIME_CANCELLED"&&terminal.CurrentContentId==baseline,"CANCELLED_REPORT_TERMINAL_MISMATCH");
  Need(!File.Exists(Path.Combine(caseRoot,"ui-request.claim.json")),"CANCELLED_REPORT_OWNED_CLAIM_LEAKED");
  queued.Frame(()=>ownedDriver.Tick(true,false,2*Stopwatch.Frequency));Need(ownedFeatures.SelectedCalls==0,"CANCELLED_REPORT_LATE_COMMIT");
 }
 else
 {
  Need(runtime.Selected!.Identity==baseline&&ownedFeatures.SelectedCalls==0,"WORKER_FINISH_SELECTED_WITHOUT_COMMIT_FRAME");
  Need(transport.LastResult()?.Status=="PREPARED","PREPARED_REPORT_NOT_PERSISTED_BEFORE_COMMIT");
  queued.Frame(()=>ownedDriver.Tick(true,false,2*Stopwatch.Frequency));await PumpOn(queued,ownedDriver.Worker);
  var terminal=transport.LastResult();Need(ownedFeatures.SelectedCalls==1&&runtime.Selected!.Identity==staged.ContentId,"PREPARED_REPORT_NO_SINGLE_COMMIT");
  Need(terminal?.RequestId==requestId&&terminal.Status=="COMMITTED"&&terminal.CurrentContentId==staged.ContentId,"COMMITTED_REPORT_TERMINAL_MISMATCH");
  Need(!File.Exists(Path.Combine(caseRoot,"ui-request.claim.json")),"COMMITTED_REPORT_OWNED_CLAIM_LEAKED");
 }
 Need(pendingBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(caseRoot,"pending_restart.json"))),"RACE_TERMINAL_PENDING_CHANGED");
}
await Check("strict local signal keys/types/value/size with no path or executable",()=>{
 var valid=new LocalUiRequest(1,Guid.NewGuid().ToString("N"),new string('a',64),new string('b',64),"ui_reload");var bytes=JsonSerializer.SerializeToUtf8Bytes(valid);Need(LocalUiRequestChannel.Parse(bytes)==valid,"VALID_SIGNAL_REJECTED");
 foreach(var bad in new[]{"{}","[]",System.Text.Encoding.UTF8.GetString(bytes).Replace("\"Operation\"","\"Path\""),System.Text.Encoding.UTF8.GetString(bytes).Replace("ui_reload","exec"),new string('x',513)}){bool rejected=false;try{LocalUiRequestChannel.Parse(System.Text.Encoding.UTF8.GetBytes(bad));}catch{rejected=true;}Need(rejected,"INVALID_SIGNAL_ACCEPTED");}return Task.CompletedTask;});
await Check("actual store startup under queued CPU main thread",async()=>{var t=coordinator.StartAsync(initial);await Pump(t);Need(t.Result.Committed,"START_FAILED");Need(features.Initializations==1,"DUPLICATE_INIT");Need(await features.Session!.GetMasterAsync()!=null,"ACTUAL_MANAGED_SESSION_MASTER");});
var original=coordinator.Selected!.Identity;var session=features.Session;var masterTask=session!.GetMasterAsync();var namesTask=session.GetNamesAsync();await namesTask;
var beforeCurrent=File.ReadAllBytes(Path.Combine(root,"current.json"));
await Check("default opt-in OFF and enabled idle polling throttled",async()=>{for(int i=0;i<200;i++)Frame(false);Need(driver.Polls==0,"DISABLED_DISK_POLL");Frame();await Pump(driver.Worker);long count=driver.Polls;for(int i=0;i<200;i++)scheduler.Frame(()=>driver.Tick(true,false,frameTimestamp-Stopwatch.Frequency));Need(driver.Polls==count,"UNTHROTTLED_POLL");});
await Check("actual request-ui prepares complete package without selecting or consuming pending",async()=>{
 File.WriteAllText(Path.Combine(root,"pending_restart.json"),JsonSerializer.Serialize(new PendingRestartSelection(1,original,original,"CPU_SENTINEL")));var pending=File.ReadAllBytes(Path.Combine(root,"pending_restart.json"));
 var cli=await Cli(changed,original);Need(cli.GetProperty("request_saved").GetBoolean(),"CLI_REQUEST_NOT_SAVED:"+cli.GetProperty("reason"));Need(cli.GetProperty("selection_records_unchanged").GetBoolean(),"CLI_POINTER_CHANGED");Need(File.ReadAllBytes(Path.Combine(root,"current.json")).SequenceEqual(beforeCurrent),"PREPARE_PUBLISHED_CURRENT");Need(File.ReadAllBytes(Path.Combine(root,"pending_restart.json")).SequenceEqual(pending),"PENDING_CHANGED");});
await Check("Choice then owned restore retry preserves old selection then one main-thread UI commit",async()=>{
 features.ChoiceBusy=true;await Until(()=>driver.LastOutcome?.Status=="WAITING");Need(driver.LastOutcome!.Reason=="CHOICE_SAFE_BOUNDARY_DEFERRED","WRONG_CHOICE_WAIT");Need(coordinator.Selected!.Identity==original,"EARLY_CHOICE_SELECTION");
 features.ChoiceBusy=false;features.RestoreReady=false;Frame();await Pump(driver.Worker);Need(driver.LastOutcome!.Reason=="NATIVE_DISPLAY_RESTORE_DEFERRED","RESTORE_NOT_DEFERRED");Need(coordinator.Selected!.Identity==original,"EARLY_RESTORE_SELECTION");
 features.RestoreReady=true;Frame();await Pump(driver.Worker);Need(driver.LastOutcome!.Status=="COMMITTED","NO_COMMIT");Need(coordinator.Selected!.Resources[fixture.GetProperty("resource_id").GetString()!].Ko=="닫기닫기","WRONG_ACTIVE_KO");Need(features.SelectedCalls==1,"DOUBLE_COMMIT");Need(ReferenceEquals(session,features.Session)&&ReferenceEquals(masterTask,session.GetMasterAsync())&&ReferenceEquals(namesTask,session.GetNamesAsync()),"REAL_MANAGED_SESSION_OR_CACHE_REFRESH");Need(store.ReadPendingRestart()?.Reason=="CPU_SENTINEL","UI_CONSUMED_PENDING");Need(!File.Exists(Path.Combine(root,"ui-request.claim.json")),"OWNED_CLAIM_NOT_FINISHED");});
await Check("duplicate and stale/busy are bounded and preserve current",async()=>{
 var current=coordinator.Selected!.Identity;var last=channel.LastResult()!;var duplicate=channel.Publish(new(1,last.RequestId!,last.CandidateContentId!,original,"ui_reload"));Need(duplicate=="DUPLICATE_REQUEST","DUPLICATE_NOT_ISOLATED");
 Need(channel.Publish(new(1,Guid.NewGuid().ToString("N"),original,original,"ui_reload"))=="STALE_EXPECTED_CURRENT","STALE_PUBLISHED");
 var restored=await store.StageAsync(fixture.GetProperty("restored").GetString()!,coordinator.Selected);var ticket=new LocalUiRequest(1,Guid.NewGuid().ToString("N"),restored.ContentId,current,"ui_reload");Need(channel.Publish(ticket)=="UI_REQUEST_PUBLISHED","BUSY_FIXTURE_PUBLISH");Frame(otherBusy:true);await Pump(driver.Worker);Need(driver.LastOutcome?.Reason=="UI_REQUEST_BUSY","BUSY_NOT_REJECTED");Need(coordinator.Selected!.Identity==current,"BUSY_SELECTED");});
await Check("RESTART_REQUIRED does not overwrite/consume existing pending",async()=>{
 var prepared=await coordinator.PrepareStoredUiAsync(original);Need(prepared!=null,"PREPARE_REQUIRED");var s=prepared!.Snapshot;
 // Synthetic branch fixture only. Production snapshots remain immutable; no native proof is fabricated.
 var clone=(TranslationPackageSnapshot)Activator.CreateInstance(typeof(TranslationPackageSnapshot),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object?[]{s.Origin,s.Revision,s.Locale,s.Identity,s.Generations,s.Resources.Values,s.Bindings.Values,s.Files,s.ComponentSignatures.SetItem("master","fixture-authority-change"),s.Master,s.Validation,s.PreviousIdentity,s.LayoutPolicies.Values,s.Payloads},null)!;
 var changedAuthority=prepared with{Snapshot=clone};
 var pending=File.ReadAllBytes(Path.Combine(root,"pending_restart.json"));RuntimeCommitResult? outcome=null;scheduler.Frame(()=>outcome=coordinator.CommitUiRequest(changedAuthority));Need(outcome!.Reason=="RESTART_REQUIRED","AUTHORITY_CHANGE_LIVE");Need(pending.SequenceEqual(File.ReadAllBytes(Path.Combine(root,"pending_restart.json"))),"RESTART_PENDING_OVERWRITTEN");});
await Check("foreign replacement and oversize claim are never deleted",()=>{
 var external=Path.Combine(scratch,"foreign-custody");Directory.CreateDirectory(external);var ch=new LocalUiRequestChannel(external);File.WriteAllText(Path.Combine(external,"ui-request.json"),"{}");var claim=ch.TryClaim()!;File.WriteAllText(Path.Combine(external,"ui-request.claim.json"),"{\"external\":1}");Need(!ch.Report(claim,new(1,null,null,null,"REJECTED","INVALID"),true),"FOREIGN_FILE_CLAIMED");Need(File.Exists(Path.Combine(external,"ui-request.claim.json")),"FOREIGN_FILE_DELETED");
 File.WriteAllText(Path.Combine(external,"ui-request.claim.json"),new string('x',700));var huge=ch.TryClaim()!;File.WriteAllText(Path.Combine(external,"ui-request.claim.json"),new string('y',701));ch.Report(huge,new(1,null,null,null,"REJECTED","UI_REQUEST_SIZE"),true);Need(File.ReadAllText(Path.Combine(external,"ui-request.claim.json"))==new string('y',701),"OVERSIZE_FOREIGN_DELETED");File.WriteAllText(Path.Combine(external,"current.json"),"{\"Schema\":999999999999999999,\"ContentId\":\""+new string('a',64)+"\"}");Need(ch.CurrentId()==null,"MALFORMED_POINTER_ACCEPTED");return Task.CompletedTask;});
await Check("actual PREPARED report OS lock blocks commit until worker completion",()=>PreparedPublicationRace(false));
await Check("cancel at actual PREPARED report OS lock keeps old current and terminal consistent",()=>PreparedPublicationRace(true));
await Check("foreign claim replacement at actual PREPARED report discards preparation without selecting or deleting foreign bytes",()=>PreparedPublicationRace(false,true));
await Check("cancellation after claim before main-thread callback leaves no late native commit",async()=>{
 var id=Guid.NewGuid().ToString("N");Need(channel.Publish(new(1,id,original,coordinator.Selected!.Identity,"ui_reload"))=="UI_REQUEST_PUBLISHED","CANCEL_PUBLISH");Frame();
 var timer=Stopwatch.StartNew();while(!File.Exists(Path.Combine(root,"ui-request.claim.json"))){Need(timer.Elapsed<TimeSpan.FromSeconds(10),"CANCEL_CLAIM_TIMEOUT");await Task.Delay(1);}
 string selected=coordinator.Selected!.Identity;driver.Dispose();await Pump(driver.Worker);Need(coordinator.Selected!.Identity==selected,"CANCEL_SELECTED");Need(!File.Exists(Path.Combine(root,"ui-request.claim.json")),"CANCEL_OWNED_CLAIM_LEAKED");
});
await Check("unload gate discards late tick and pending callbacks",async()=>{long polls=driver.Polls;driver.Dispose();for(int i=0;i<20;i++)Frame();Need(driver.Polls==polls,"LATE_POLL");await Pump(driver.Worker);scheduler.Frame(()=>Need(coordinator.TryUnload(),"UNLOAD_FAILED"));Need(features.Initializations==1,"LATE_NATIVE_INIT");});
var report=new{schema="gc-ng08-ui-request-actual-product-cpu-v1",status=failed==0?"PASS":"FAIL",count=results.Count,failures=failed,results,assembly_sha256=PackageLoader.Hash(File.ReadAllBytes(typeof(RuntimeCoordinator).Assembly.Location)),actual_candidate_DLL=true,actual_CLI=true,actual_immutable_store=true,real_managed_TranslationSession=true,native_feature_provider="CPU_MOCK_NO_UNITY",main_thread="QUEUED_CPU_SCHEDULER",native_calls=0,game="NOT_RUN_IN_CPU_SUITE",authority_restart_case="synthetic snapshot signature mutation tests real CommitUiRequest boundary; not a new semantic decision"};File.WriteAllText(args[3],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"NG08 REQUEST CPU {(failed==0?"PASS":"FAIL")} {results.Count-failed}/{results.Count}");return failed==0?0:1;

sealed class Scheduler:IRuntimeMainThreadScheduler
{
 [ThreadStatic]static bool main;readonly ConcurrentQueue<Action> queue=new();public bool IsMainThread=>main;
 public Task<T> InvokeAsync<T>(Func<T> callback,CancellationToken cancellationToken=default){if(main){cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(callback());}var done=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);var reg=cancellationToken.Register(()=>done.TrySetCanceled(cancellationToken));_ = done.Task.ContinueWith(_=>reg.Dispose());queue.Enqueue(()=>{if(done.Task.IsCompleted)return;try{done.SetResult(callback());}catch(Exception e){done.TrySetException(e);}});return done.Task;}
 public void Frame(Action action){bool before=main;main=true;try{action();}finally{main=before;}}
 public int Pending=>queue.Count;
 public bool PumpOne(){bool ran=false;Frame(()=>{if(queue.TryDequeue(out var a)){ran=true;a();}});return ran;}
 public void Pump()=>Frame(()=>{while(queue.TryDequeue(out var a))a();});
}
sealed class Features:IRuntimeFeatureProvider,IRuntimeDisplayBoundary
{
 TranslationPackageSnapshot? snapshot;public TranslationSession? Session;public int Initializations,SelectedCalls;public bool ChoiceBusy,RestoreReady=true;
 public bool IsChoiceBusy=>ChoiceBusy;public IMasterTranslationView MasterView=>snapshot?.Master??MasterTranslationView.Empty;
 public void Initialize(TranslationPackageSnapshot s,UiModuleHost host){Initializations++;snapshot=s;Session=new(new LocalPackageTranslationStore(s));}
 public void Selected(TranslationPackageSnapshot s,UiModuleHost host){SelectedCalls++;snapshot=s;}
 public bool TryRestoreDisplay()=>RestoreReady;public bool TryUninstall()=>true;public void Dispose()=>Session?.Dispose();
}
