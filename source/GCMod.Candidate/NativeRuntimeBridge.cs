#nullable enable
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GCMod.Nextgen;
using GCMod.Candidate.Native;
namespace GCMod.Candidate;
/// Real Plugin→local donor session→shared native providers. Opt-in is a runtime configuration gate.
public sealed class NativeRuntimeBridge {
 readonly GameThreadGate gate=new(); readonly MainThreadScheduler scheduler; readonly SafeDiagnostics diagnostics;
 readonly ConfigEntry<bool> enabled,reload,checkUpdate,uiRequestsEnabled; readonly ConfigEntry<string> packageRoot,updateRepository,updateChannel;
 readonly HttpClient updateClient; readonly FixedRefDataUpdater updater;
 readonly RuntimeCoordinator coordinator; readonly Features features; readonly CancellationTokenSource cancel=new();
 readonly LocalUiRequestDriver uiRequests;
 readonly RuntimeStartupAttemptGate startup=new(); bool preparing,disposed; PreparedPackage? pending;
 public NativeRuntimeBridge(ConfigFile config,ManualLogSource log){
  enabled=config.Bind("Nextgen","Enabled",false,"Opt in to the offline operator-ready NG05 candidate. Existing frozen installation is preserved outside this candidate.");
  packageRoot=config.Bind("Nextgen","PackageRoot","GCMod.Nextgen.Candidate/translations/ko","Local authoring package root, relative to BepInEx plugins or absolute. No remote source is substituted.");
  reload=config.Bind("Nextgen","ReloadPackage",false,"Set true to explicitly retry failed startup or prepare one local authoring package reload; safe main-thread commit may wait for Choice or restore.");
  checkUpdate=config.Bind("Nextgen","CheckUpdate",false,"Set true for one worker fixed-ref JSON data check. Runtime commit waits for a safe main-thread boundary; authority changes await restart.");
  uiRequestsEnabled=config.Bind("Nextgen","UiRequestsEnabled",false,"Opt in to bounded local UpdateCli request-ui signals. Complete immutable JSON candidates only; default OFF. No network, installer, or native command input.");
  updateRepository=config.Bind("Nextgen","UpdateRepository","kekwkekw/gctrans","Trusted JSON data repository. Public publication is a separate operation.");
  updateChannel=config.Bind("Nextgen","UpdateChannel","main","Resolve this branch/tag once per explicit check; every file uses that immutable commit.");
  diagnostics=new(sink:c=>log.LogInfo(c));updateClient=FixedRefDataUpdater.CreateClient();updater=new(updateClient,Path.Combine(Paths.ConfigPath,"GCMod.Nextgen.local","transport"),diagnostics);scheduler=new(gate);var registry=AdapterRegistry.CreateNg01();var ownership=new UiOwnershipService(diagnostics);var fonts=new FontService();
  features=new(gate,registry,ownership,fonts,diagnostics);var loader=new PackageLoader(registry);
  var store=new LocalImmutablePackageStore(Path.Combine(Paths.ConfigPath,"GCMod.Nextgen.local"),loader);
  coordinator=new(loader,registry,ownership,scheduler,features,store,fonts,diagnostics);
  uiRequests=new(coordinator,scheduler,new LocalUiRequestChannel(store.Root),diagnostics);
 }
 string PackageRoot=>Path.IsPathRooted(packageRoot.Value)?Path.GetFullPath(packageRoot.Value):Path.GetFullPath(Path.Combine(Paths.PluginPath,packageRoot.Value));
 public void Tick(){if(disposed)return;gate.Capture();scheduler.Tick();GCMod.Config.TickSettings(gate.CanAccess);
  if(coordinator.Host==null){bool retry=reload.Value;if(startup.TryBegin(enabled.Value,retry)){if(retry)reload.Value=false;Observe(Start());}}
  if(coordinator.Host!=null){features.Tick();if(checkUpdate.Value&&!preparing&&pending==null&&!uiRequests.IsBusy){checkUpdate.Value=false;preparing=true;Observe(PrepareUpdate());}if(reload.Value&&!preparing&&pending==null&&!uiRequests.IsBusy){reload.Value=false;preparing=true;Observe(PrepareReload());}}
  if(pending!=null){var result=coordinator.CommitUi(pending);if(result.Committed||result.Reason is "RESTART_REQUIRED" or "STALE_OR_CANCELLED_PREPARATION" or "SAME_SELECTION_NOOP"){diagnostics.Record(result.Reason);pending=null;}}
  uiRequests.Tick(enabled.Value&&uiRequestsEnabled.Value&&coordinator.Host!=null,preparing||pending!=null);
 }
 async Task Start(){try{var result=await Task.Run(()=>coordinator.StartAsync(PackageRoot,cancel.Token),cancel.Token).ConfigureAwait(false);diagnostics.Record(result.Reason);}finally{await scheduler.InvokeAsync(()=>{startup.Complete();return true;},cancel.Token).ConfigureAwait(false);}}
 async Task PrepareUpdate(){try{var source=new UpdateSource(updateRepository.Value,updateChannel.Value);var result=await Task.Run(()=>updater.PrepareAsync(source,coordinator,cancel.Token),cancel.Token).ConfigureAwait(false);diagnostics.Record(result.Reason);await scheduler.InvokeAsync(()=>{pending=result.Prepared;preparing=false;return true;},cancel.Token).ConfigureAwait(false);}catch(OperationCanceledException){}catch{diagnostics.Record("UPDATE_PREPARE_FAILURE_SELECTION_RETAINED");await scheduler.InvokeAsync(()=>{preparing=false;return true;},cancel.Token).ConfigureAwait(false);}}
 async Task PrepareReload(){try{var prepared=await Task.Run(()=>coordinator.PrepareAsync(PackageRoot,cancel.Token),cancel.Token).ConfigureAwait(false);await scheduler.InvokeAsync(()=>{pending=prepared;preparing=false;return true;},cancel.Token).ConfigureAwait(false);}catch(OperationCanceledException){}catch{diagnostics.Record("RELOAD_PREPARE_FAILURE_ISOLATED");await scheduler.InvokeAsync(()=>{preparing=false;return true;},cancel.Token).ConfigureAwait(false);}}
 async void Observe(Task task){try{await task.ConfigureAwait(false);}catch(OperationCanceledException){}catch{diagnostics.Record("RUNTIME_START_FAILURE_ISOLATED");}}
 public bool TryUnload(Action? releaseOwnedDriver=null){if(disposed)return true;if(!gate.CanAccess)return false;if(!coordinator.TryUnload())return false;try{gate.Require();releaseOwnedDriver?.Invoke();}catch{diagnostics.Record("OWNED_DRIVER_CLEANUP_DEFERRED");return false;}GCMod.Config.Unsubscribe();disposed=true;uiRequests.Dispose();cancel.Cancel();cancel.Dispose();updateClient.Dispose();gate.Close();return true;}
 sealed class ActiveMasterProxy:IMasterTranslationView,IActiveMasterOutputView {
  public LegacyTranslationRuntime? Legacy;
  public bool TryResolve(MasterRef r,string role,out MasterValue value){value=new("","not-ready",0);return Legacy?.MasterView.TryResolve(r,role,out value)==true;}
  public bool ContainsOutput(string table,string field,string ko)=>Legacy?.MasterView is IActiveMasterOutputView output&&output.ContainsOutput(table,field,ko);
 }
 sealed class Features:IRuntimeFeatureProvider,IRuntimeDisplayBoundary,IRuntimeUnloadBoundary,IRuntimeInitializationBoundary {
  readonly GameThreadGate gate;readonly AdapterRegistry registry;readonly UiOwnershipService ownership;readonly NativeFontProvider fonts;readonly SafeDiagnostics log;
  readonly ActiveMasterProxy master=new(); readonly UiFloatOwnershipService sizes=new();NativeHookHub? hub;GeneralUiProvider? general;StoryNativeProvider? story;BattleNativeProvider? battle;Fit3NativeProvider? fit;
  TranslationPackageSnapshot? snapshot;UiModuleHost? selectedHost;bool mediumPublished;
  public Features(GameThreadGate gate,AdapterRegistry registry,UiOwnershipService ownership,FontService fontService,SafeDiagnostics log){this.gate=gate;this.registry=registry;this.ownership=ownership;fonts=new(gate,fontService);this.log=log;}
  public IMasterTranslationView MasterView=>master;
  public bool IsChoiceBusy=>story?.IsChoiceBusy==true;
  public bool CanUnload=>master.Legacy?.CanUnload??true;
  static bool WritesEnabled()=>SpecialDisplayPolicy.AllowsWrites(GCMod.Config.Translation.Value,GCMod.Config.TranslationLanguage.Value);
  public void Initialize(TranslationPackageSnapshot snapshot,UiModuleHost host){gate.Require();this.snapshot=snapshot;selectedHost=host;master.Legacy=new(snapshot);master.Legacy.Initialize();hub=new(gate,log);general=new(hub,gate,registry,ownership,fonts,log,sizes);general.Initialize(snapshot,host);
   story=new(gate,hub,registry,ownership,fonts,()=>this.snapshot!,log,master,WritesEnabled);story.Initialize();battle=new(gate,hub,registry,ownership,fonts,()=>this.snapshot!,log,master,WritesEnabled);battle.Initialize();
   fit=new(gate,hub,sizes,fonts,()=>this.snapshot!,ko=>master.ContainsOutput("mNovels","title",ko)||master.ContainsOutput("mNovels","ml_title[]",ko),log,WritesEnabled);fit.Initialize();PublishFont();
  }
  void PublishFont(){gate.Require();var font=GCMod.Plugin.Trans?.Font?.Asset;if(font!=null&&fonts.TryPublish(font)&&!mediumPublished){mediumPublished=true;log.Record("NATIVE_MEDIUM_PUBLISHED");}fonts.Tick();}
  public void Tick(){gate.Require();PublishFont();
   bool enabled=WritesEnabled();
   // Settings/catalog reconciliation runs independently of JP-only discovery and settled geometry tickets.
   bool display=(story?.ReconcileDisplay()??true)&(battle?.ReconcileDisplay()??true)&(fit?.ReconcileDisplay()??true);
   if(!enabled){bool generalRestored=general?.RestoreOwned()??true;var text=selectedHost?.ReconcileUnavailableResources(false,gate.CanAccess);
    if(!display||!generalRestored||text?.Committed==false)log.Record("TRANSLATION_DISABLED_OWNED_RESTORE_DEFERRED");return;}
   var selective=selectedHost?.ReconcileUnavailableResources(true,gate.CanAccess);if(selective?.Committed==false)log.Record("UNAVAILABLE_RESOURCE_OWNED_RESTORE_DEFERRED");
   general?.Tick();story?.Tick();battle?.Tick();fit?.Tick();}
  public bool TryRestoreDisplay(){gate.Require();if(IsChoiceBusy)return false;return (general?.TryRestoreDisplay()??true)&&(story?.RestoreOwned()??true)&&(battle?.RestoreOwned()??true)&&(fit?.RestoreOwned()??true);}
  public void Selected(TranslationPackageSnapshot next,UiModuleHost host){gate.Require();var session=master.Legacy?.SessionIdentity;master.Legacy?.SelectUi(next);log.Record(session!=null&&ReferenceEquals(session,master.Legacy?.SessionIdentity)?"UI_LEGACY_SESSION_PRESERVED":"UI_LEGACY_SESSION_UNAVAILABLE");snapshot=next;selectedHost=host;general?.Selected(next,host);story?.Selected();battle?.Selected();fit?.Selected();}
  public bool TryUninstall(){gate.Require();if(!TryRestoreDisplay()||general?.RestoreOwned()==false)return false;
   if(master.Legacy?.TryUninstall()==false)return false;return hub?.TryUninstall()??true;}
  public bool TryResetFailedInitialization(){gate.Require();if(!TryUninstall())return false;Dispose();return true;}
  public void Dispose(){gate.Require();general?.Dispose();story?.Dispose();battle?.Dispose();fit?.Dispose();hub?.Dispose();fonts.Dispose();master.Legacy?.Dispose();master.Legacy=null;general=null;story=null;battle=null;fit=null;hub=null;snapshot=null;selectedHost=null;}
 }
}
