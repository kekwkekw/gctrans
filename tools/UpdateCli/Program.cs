using System.Text.Json;
using GCMod.Nextgen;

var options = new JsonSerializerOptions { WriteIndented = true };
if (args.Length < 3 || args[0] is not ("prepare" or "prepare-local" or "request-ui" or "status") || args[1] != "--store")
{
    Console.Error.WriteLine("Usage: UpdateCli status --store PATH | request-ui --store PATH --package NEXT_PACKAGE --based-on CONTENT_ID [--request-id GUID_N] | prepare-local --store ISOLATED_PATH --package NEXT_PACKAGE [--based-on CONTENT_ID] | prepare --store PATH [--repository owner/repo] [--channel main]");
    Environment.ExitCode = 2;
    return;
}
var values = new Dictionary<string,string>(StringComparer.Ordinal);
for (int i=1;i<args.Length;i+=2)
{
    if (i+1>=args.Length || args[i] is not ("--store" or "--repository" or "--channel" or "--package" or "--based-on" or "--request-id") || !values.TryAdd(args[i],args[i+1]))
    { Console.Error.WriteLine("Invalid or duplicate argument."); Environment.ExitCode=2;return; }
}
var root = Path.GetFullPath(values["--store"]);
var registry = AdapterRegistry.CreateNg01();
var loader = new PackageLoader(registry);
var store = new LocalImmutablePackageStore(root,loader);
byte[]? Pointer(string name) { try { return File.ReadAllBytes(Path.Combine(root,name)); } catch(FileNotFoundException) { return null; } catch(DirectoryNotFoundException) { return null; } }
object? PointerView(byte[]? bytes) { try { return bytes==null?null:JsonSerializer.Deserialize<JsonElement>(bytes); } catch(JsonException) { return new { status="INVALID_SELECTION_RECORD" }; } }
if(args[0]=="request-ui")
{
    if(!values.TryGetValue("--package",out var package)||!values.TryGetValue("--based-on",out var expected)||values.ContainsKey("--repository")||values.ContainsKey("--channel"))
    {Console.Error.WriteLine("request-ui requires --package and --based-on; no network source permitted.");Environment.ExitCode=2;return;}
    var channel=new LocalUiRequestChannel(root);string reason="UI_REQUEST_REJECTED";LocalUiRequest? request=null;string? candidate=null;
    var beforeCurrent=Pointer("current.json");var beforePrevious=Pointer("previous.json");var beforePending=Pointer("pending_restart.json");
    bool preserved=true;
    try
    {
        // Validate external request identifiers before package work. No executable/path fields enter the signal.
        string id=values.GetValueOrDefault("--request-id",Guid.NewGuid().ToString("N"));
        _ = LocalUiRequestChannel.Parse(JsonSerializer.SerializeToUtf8Bytes(new LocalUiRequest(1,id,expected,expected,"ui_reload")));
        var active=await store.RecoverAsync();
        if(active==null||active.ContentId!=expected||channel.CurrentId()!=expected)reason="STALE_OR_MISSING_CURRENT_SELECTION";
        else
        {
            var ready=await store.StageAsync(Path.GetFullPath(package),active.Snapshot);candidate=ready.ContentId;
            if(RuntimeCoordinator.RequiresRestart(active.Snapshot,ready.Snapshot))reason="RESTART_REQUIRED";
            else if(candidate==expected)reason="SAME_SELECTION_NOOP";
            else
            {
                preserved=(beforeCurrent??[]).SequenceEqual(Pointer("current.json")??[])&&(beforePrevious??[]).SequenceEqual(Pointer("previous.json")??[])&&(beforePending??[]).SequenceEqual(Pointer("pending_restart.json")??[]);
                if(!preserved)reason="SELECTION_CHANGED_DURING_REQUEST_PREPARATION";
                else {request=new(1,id,candidate,expected,"ui_reload");reason=channel.Publish(request);}
            }
        }
    }
    catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or PackageValidationException or InvalidOperationException or ArgumentException)
    {reason="UI_REQUEST_IO_OR_CONTRACT_REJECTED";}
    bool currentChanged=!(beforeCurrent??[]).SequenceEqual(Pointer("current.json")??[]),previousChanged=!(beforePrevious??[]).SequenceEqual(Pointer("previous.json")??[]),pendingChanged=!(beforePending??[]).SequenceEqual(Pointer("pending_restart.json")??[]);
    preserved&=!currentChanged&&!previousChanged&&!pendingChanged;
    Console.WriteLine(JsonSerializer.Serialize(new{schema="gc-ng08-local-ui-request-v1",reason,request,candidate,request_saved=reason=="UI_REQUEST_PUBLISHED",selection_records_unchanged=preserved,current_changed=currentChanged,previous_changed=previousChanged,pending_restart_changed=pendingChanged,request_owned_pending_writes=false,native_initialize="NOT_RUN",live_delivery="NOT_RUN_CLI_ONLY",network="NOT_PERFORMED",next=reason=="RESTART_REQUIRED"?"use prepare-local/pending_restart after appropriate restart authorization":"enabled existing native driver receives the bounded signal; publication alone is not commit/display"},options));
    Environment.ExitCode=reason=="UI_REQUEST_PUBLISHED"&&preserved?0:2;return;
}
if(values.ContainsKey("--request-id")){Console.Error.WriteLine("--request-id is only supported by request-ui.");Environment.ExitCode=2;return;}
if (args[0]=="prepare-local")
{
    if (!values.TryGetValue("--package",out var package) || values.ContainsKey("--repository") || values.ContainsKey("--channel"))
    { Console.Error.WriteLine("prepare-local requires --package and takes no network source.");Environment.ExitCode=2;return; }
    string reason="LOCAL_CANDIDATE_REJECTED";bool saved=false;string? candidate=null;string? basedOn=null;
    byte[]? beforeCurrent=null;byte[]? beforePrevious=null;bool currentChanged=false;bool previousChanged=false;
    try
    {
        beforeCurrent=Pointer("current.json");beforePrevious=Pointer("previous.json");
        using var localCoordinator=new RuntimeCoordinator(loader,registry,new UiOwnershipService(),new PreparationOnlyScheduler(),new PreparationOnlyFeatures(),store);
        basedOn=(await localCoordinator.RecoverPreparationBaselineAsync())?.Identity;
        if(values.TryGetValue("--based-on",out var expected) && expected!=(basedOn??"NONE")) reason="EXPECTED_CURRENT_CONFLICT";
        else
        {
            var prepared=await localCoordinator.PrepareAsync(Path.GetFullPath(package));candidate=prepared?.Snapshot.Identity;
            if(prepared!=null)
            {
                var after=(await localCoordinator.RecoverPreparationBaselineAsync())?.Identity;
                bool sameCurrent=(beforeCurrent??Array.Empty<byte>()).SequenceEqual(Pointer("current.json")??Array.Empty<byte>());
                bool samePrevious=(beforePrevious??Array.Empty<byte>()).SequenceEqual(Pointer("previous.json")??Array.Empty<byte>());
                if(after!=basedOn || !sameCurrent || !samePrevious) reason="CURRENT_CHANGED_DURING_PREPARATION";
                else { saved=localCoordinator.SavePendingStartup(prepared,basedOn);reason=saved?"LOCAL_COMPLETE_PENDING_SAVED":"PENDING_SAVE_FAILED_CURRENT_RETAINED"; }
            }
        }
        currentChanged=!(beforeCurrent??Array.Empty<byte>()).SequenceEqual(Pointer("current.json")??Array.Empty<byte>());
        previousChanged=!(beforePrevious??Array.Empty<byte>()).SequenceEqual(Pointer("previous.json")??Array.Empty<byte>());
    }
    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or PackageValidationException or InvalidOperationException or ArgumentException)
    { reason="LOCAL_PREPARATION_IO_OR_CONTRACT_FAILED"; }
    Console.WriteLine(JsonSerializer.Serialize(new { schema="gc-ng05-local-preparation-v1", reason, based_on=basedOn, candidate,
        pending_saved=saved, pending=store.ReadPendingRestart(),current_changed=currentChanged,previous_changed=previousChanged,
        current=PointerView(beforeCurrent),previous=PointerView(beforePrevious),
        native_initialize="NOT_RUN",native="NOT_RUN",game="NOT_RUN",installation="NOT_PERFORMED",network="NOT_PERFORMED" },options));
    Environment.ExitCode=saved && !currentChanged && !previousChanged?0:1;return;
}
if (args[0] == "status")
{
    var valid = await store.RecoverAsync();
    var pending = store.ReadPendingRestart();
    object? Selection(string name)
    {
        try
        {
            using var json = JsonDocument.Parse(ReadPointerBytes(name));
            var value = json.RootElement;
            return new { schema=value.GetProperty("Schema").GetInt32(), content_id=value.GetProperty("ContentId").GetString() };
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }
    byte[] ReadPointerBytes(string name) => File.ReadAllBytes(Path.Combine(root,name));
    Console.WriteLine(JsonSerializer.Serialize(new { current=Selection("current.json"), previous=Selection("previous.json"), valid_lkg=valid?.ContentId,ui_request_last=new LocalUiRequestChannel(root).LastResult(),
        pending_restart=pending, native="NOT_RUN", action=pending==null?"prepare when a reviewed release is available":"next normal startup validates and initializes pending, or keeps valid LKG" },options));
    return;
}
using var client=FixedRefDataUpdater.CreateClient();
using var cancellation=new CancellationTokenSource();
Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;cancellation.Cancel();};
var coordinator=new RuntimeCoordinator(loader,registry,new UiOwnershipService(),new PreparationOnlyScheduler(),new PreparationOnlyFeatures(),store);
var updater=new FixedRefDataUpdater(client,Path.Combine(root,"downloads"));
var result=await updater.PrepareAsync(new(values.GetValueOrDefault("--repository","kekwkekw/gctrans"),values.GetValueOrDefault("--channel","main")),coordinator,cancellation.Token);
bool pendingSaved=false;
if(result.Prepared!=null && result.Reason!="UPDATE_NO_CHANGE")
{
    // Preparing bytes is read-only with respect to current. Protect a concurrent operational selection before recording pending.
    var current=await coordinator.RecoverPreparationBaselineAsync(cancellation.Token);
    if(current?.Identity==result.BasedOnIdentity)pendingSaved=coordinator.SavePendingStartup(result.Prepared,result.BasedOnIdentity);
}
Console.WriteLine(JsonSerializer.Serialize(new { result.Reason, result.TransportCommit, result.BasedOnIdentity,
    candidate=result.Prepared?.Snapshot.Identity, pending_saved=pendingSaved, result.Metrics, current_changed=false,
    native="NOT_RUN", installation="NOT_PERFORMED" },options));
Environment.ExitCode=result.Prepared==null || result.Reason!="UPDATE_NO_CHANGE"&&!pendingSaved?1:0;

sealed class PreparationOnlyScheduler : IRuntimeMainThreadScheduler
{
    public bool IsMainThread=>false;
    public Task<T> InvokeAsync<T>(Func<T> callback,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("OFFLINE_CLI_HAS_NO_NATIVE_COMMIT_SCHEDULER");
}
sealed class PreparationOnlyFeatures : IRuntimeFeatureProvider
{
    public IMasterTranslationView MasterView=>MasterTranslationView.Empty;
    public bool IsChoiceBusy=>false;
    public void Initialize(TranslationPackageSnapshot snapshot,UiModuleHost host)=>throw new InvalidOperationException("OFFLINE_CLI_CANNOT_INITIALIZE_NATIVE");
    public void Selected(TranslationPackageSnapshot snapshot,UiModuleHost host)=>throw new InvalidOperationException("OFFLINE_CLI_CANNOT_SELECT_NATIVE");
    public bool TryUninstall()=>true;
    public void Dispose(){}
}
