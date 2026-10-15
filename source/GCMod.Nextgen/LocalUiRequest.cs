#nullable enable
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace GCMod.Nextgen;

public sealed record LocalUiRequest(int Schema,string RequestId,string CandidateContentId,string ExpectedCurrent,string Operation);
public sealed record LocalUiRequestOutcome(int Schema,string? RequestId,string? CandidateContentId,string? CurrentContentId,string Status,string Reason,bool Persisted=false);
public sealed record ClaimedUiRequest(LocalUiRequest? Request,string Fingerprint,string? Rejection);
/// One bounded local data signal, one last outcome. It never selects packages or accepts paths/commands.
public sealed class LocalUiRequestChannel(string root)
{
    public string Root { get; }=Path.GetFullPath(root);
    public const int MaximumBytes=512;
    readonly SemaphoreSlim localWriter=new(1);
    string Signal=>Path.Combine(Root,"ui-request.json");
    string Claim=>Path.Combine(Root,"ui-request.claim.json");
    string Result=>Path.Combine(Root,"ui-request-result.json");
    static bool Id(string? s)=>s?.Length==64&&s.All(c=>c is >= 'a' and <= 'f' or >= '0' and <= '9');
    static bool RequestId(string? s)=>s?.Length==32&&Guid.TryParseExact(s,"N",out _);
    static bool Plain(string path)=>!File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
    void RequireRoot(){if(Directory.Exists(Root)&&!Plain(Root))throw new IOException("UI_REQUEST_ROOT_LINK");}
    byte[] Read(string path,int maximum=MaximumBytes)
    {
        RequireRoot();if(!Plain(path))throw new IOException("UI_REQUEST_FILE_LINK");
        using var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
        if(f.Length is <1 || f.Length>maximum)throw new InvalidDataException("UI_REQUEST_SIZE");
        var bytes=new byte[(int)f.Length];int read=0;while(read<bytes.Length){int n=f.Read(bytes,read,bytes.Length-read);if(n==0)throw new IOException("UI_REQUEST_TRUNCATED");read+=n;}return bytes;
    }
    sealed class WriterLease(FileStream file,SemaphoreSlim gate):IDisposable{public void Dispose(){file.Dispose();gate.Release();}}
    IDisposable Lock()
    {
        if(!localWriter.Wait(TimeSpan.FromSeconds(2)))throw new IOException("UI_REQUEST_WRITER_BUSY");
        try{RequireRoot();Directory.CreateDirectory(Root);var p=Path.Combine(Root,".ui-request.lock");if(File.Exists(p)&&!Plain(p))throw new IOException("UI_REQUEST_LOCK_LINK");
            for(int attempt=0;;attempt++)try{return new WriterLease(new(p,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None),localWriter);}
            catch(IOException ex)when(attempt<40&&((ex.HResult&0xffff) is 32 or 33)){Thread.Sleep(5);}}
        catch{localWriter.Release();throw;}
    }
    public static LocalUiRequest Parse(ReadOnlyMemory<byte> bytes)
    {
        if(bytes.Length is <1 or >MaximumBytes)throw new InvalidDataException("UI_REQUEST_SIZE");
        using var d=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=4});var e=d.RootElement;
        if(e.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("UI_REQUEST_SCHEMA");
        var names=e.EnumerateObject().Select(x=>x.Name).ToArray();
        if(names.Length!=5||names.Distinct(StringComparer.Ordinal).Count()!=5||names.Except(new[]{"Schema","RequestId","CandidateContentId","ExpectedCurrent","Operation"},StringComparer.Ordinal).Any())throw new InvalidDataException("UI_REQUEST_KEYS");
        if(!e.GetProperty("Schema").TryGetInt32(out var schema)||schema!=1)throw new InvalidDataException("UI_REQUEST_SCHEMA");
        string Text(string name){var x=e.GetProperty(name);if(x.ValueKind!=JsonValueKind.String)throw new InvalidDataException("UI_REQUEST_TYPE");return x.GetString()!;}
        var r=new LocalUiRequest(schema,Text("RequestId"),Text("CandidateContentId"),Text("ExpectedCurrent"),Text("Operation"));
        if(!RequestId(r.RequestId)||!Id(r.CandidateContentId)||!Id(r.ExpectedCurrent)||r.Operation!="ui_reload")throw new InvalidDataException("UI_REQUEST_VALUE");
        return r;
    }
    public string? CurrentId()
    {
        try {using var d=JsonDocument.Parse(Read(Path.Combine(Root,"current.json")));var e=d.RootElement;var names=e.EnumerateObject().Select(x=>x.Name).ToArray();if(names.Length!=2||names.Distinct().Count()!=2||names.Except(new[]{"Schema","ContentId"}).Any())return null;var id=e.GetProperty("ContentId").GetString();return e.GetProperty("Schema").TryGetInt32(out var schema)&&schema==1&&Id(id)?id:null;}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException){return null;}
    }
    public LocalUiRequestOutcome? LastResult()
    {try{return JsonSerializer.Deserialize<LocalUiRequestOutcome>(Read(Result,1024));}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException){return null;}}
    public string Publish(LocalUiRequest request)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(request);_ = Parse(bytes);
        using var held=Lock();var last=LastResult();
        if(last?.RequestId==request.RequestId)return last.CandidateContentId==request.CandidateContentId?"DUPLICATE_REQUEST":"REQUEST_ID_CONFLICT";
        if(File.Exists(Signal)||File.Exists(Claim))return "UI_REQUEST_BUSY";
        if(CurrentId()!=request.ExpectedCurrent)return "STALE_EXPECTED_CURRENT";
        WriteAtomic(Signal,bytes,replace:false);return "UI_REQUEST_PUBLISHED";
    }
    public ClaimedUiRequest? TryClaim()
    {
        RequireRoot();if(!File.Exists(Signal)&&!File.Exists(Claim))return null;
        using var held=Lock();if(!File.Exists(Claim)){if(!File.Exists(Signal))return null;if(!Plain(Signal))throw new IOException("UI_REQUEST_FILE_LINK");File.Move(Signal,Claim);}
        byte[] bytes;
        try {bytes=Read(Claim);}catch(InvalidDataException){return new(null,"OVERSIZE","UI_REQUEST_SIZE");}
        var fingerprint=Convert.ToHexString(SHA256.HashData(bytes));
        try{return new(Parse(bytes),fingerprint,null);}catch(Exception ex)when(ex is InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException){return new(null,fingerprint,"UI_REQUEST_MALFORMED");}
    }
    public bool Report(ClaimedUiRequest claim,LocalUiRequestOutcome outcome,bool complete)
    {
        using var held=Lock();if(!File.Exists(Claim)||!Plain(Claim))return false;
        if(claim.Fingerprint=="OVERSIZE")
        {if(new FileInfo(Claim).Length<=MaximumBytes)return false;}
        else if(Convert.ToHexString(SHA256.HashData(Read(Claim)))!=claim.Fingerprint)return false;
        var bytes=JsonSerializer.SerializeToUtf8Bytes(outcome);if(bytes.Length>1024)throw new InvalidDataException("UI_RESULT_SIZE");
        if(LastResult()!=outcome)WriteAtomic(Result,bytes,true);
        // Oversize is rejected without hashing unbounded input; no proof exists to delete/rewrite it.
        if(complete&&claim.Fingerprint!="OVERSIZE")File.Delete(Claim);return true;
    }
    void WriteAtomic(string path,byte[] bytes,bool replace)
    {
        RequireRoot();if(File.Exists(path)&&!Plain(path))throw new IOException("UI_REQUEST_FILE_LINK");
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes);f.Flush(true);}File.Move(temp,path,replace);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}

/// Existing driver calls this managed state machine. All transport reads/preparation run on a worker.
public sealed class LocalUiRequestDriver(RuntimeCoordinator coordinator,IRuntimeMainThreadScheduler scheduler,LocalUiRequestChannel channel,SafeDiagnostics diagnostics):IDisposable
{
    readonly CancellationTokenSource cancellation=new();
    ClaimedUiRequest? claim;PreparedPackage? prepared;bool polling,disposed;long nextPoll;
    string? lastWait;Task work=Task.CompletedTask;
    public bool IsBusy=>polling||prepared!=null;
    public Task Worker=>work;
    public long Polls { get;private set; }
    public LocalUiRequestOutcome? LastOutcome { get;private set; }
    static bool Terminal(LocalUiRequestOutcome? o)=>o?.Status is "COMMITTED" or "REJECTED" or "NOOP";
    public void Tick(bool enabled,bool otherBusy,long timestamp=-1)
    {
        if(disposed||!scheduler.IsMainThread)return;
        // The worker must finish its PREPARED receipt before main-thread commit.
        // Otherwise unload could race a still-running PollAsync cancellation report
        // against the successful COMMITTED result for the same owned claim.
        if(polling)return;
        if(prepared!=null&&claim?.Request is {} request)
        {
            if(!enabled){Finish("REJECTED","UI_REQUEST_OPT_IN_DISABLED",false);return;}
            if(coordinator.Selected?.Identity!=request.ExpectedCurrent){Finish("REJECTED","STALE_EXPECTED_CURRENT",false);return;}
            var result=coordinator.CommitUiRequest(prepared);
            if(result.Reason is "CHOICE_SAFE_BOUNDARY_DEFERRED" or "NATIVE_DISPLAY_RESTORE_DEFERRED" or "OWNERSHIP_RESTORE_DEFERRED")
            {
                if(lastWait!=result.Reason){lastWait=result.Reason;RecordOutcome("WAITING",result.Reason,false,complete:false);}
                return;
            }
            Finish(result.Committed?"COMMITTED":result.Reason=="SAME_SELECTION_NOOP"?"NOOP":"REJECTED",result.Reason,result.Persisted);return;
        }
        if(!enabled||polling||coordinator.Selected==null)return;
        long now=timestamp<0?Stopwatch.GetTimestamp():timestamp;if(now<nextPoll)return;nextPoll=now+Stopwatch.Frequency;
        polling=true;Polls++;work=PollAsync(otherBusy);
    }
    async Task PollAsync(bool otherBusy)
    {
        var token=cancellation.Token;
        ClaimedUiRequest? found=null;
        try
        {
            found=await Task.Run(channel.TryClaim,token).ConfigureAwait(false);
            if(found==null)return;
            await scheduler.InvokeAsync(()=>{if(!disposed)claim=found;return true;},token).ConfigureAwait(false);
            var request=found.Request;
            string? rejected=found.Rejection;
            if(request!=null)
            {
                var last=await Task.Run(channel.LastResult,token).ConfigureAwait(false);
                if(last?.RequestId==request.RequestId&&Terminal(last))rejected="DUPLICATE_REQUEST";
                else if(otherBusy)rejected="UI_REQUEST_BUSY";
                else if(request.ExpectedCurrent!=coordinator.Selected?.Identity)rejected="STALE_EXPECTED_CURRENT";
            }
            if(rejected!=null){await EndAsync(found,"REJECTED",rejected,false).ConfigureAwait(false);return;}
            var next=await Task.Run(()=>coordinator.PrepareStoredUiAsync(request!.CandidateContentId,token),token).ConfigureAwait(false);
            if(next==null){await EndAsync(found,"REJECTED","UI_REQUEST_PREPARE_REJECTED",false).ConfigureAwait(false);return;}
            if(next.BasedOnIdentity!=request!.ExpectedCurrent){await EndAsync(found,"REJECTED","STALE_EXPECTED_CURRENT",false).ConfigureAwait(false);return;}
            if(coordinator.Selected is {} old&&RuntimeCoordinator.RequiresRestart(old,next.Snapshot)){await EndAsync(found,"REJECTED","RESTART_REQUIRED",false).ConfigureAwait(false);return;}
            await scheduler.InvokeAsync(()=>{if(!disposed){prepared=next;lastWait=null;LastOutcome=Outcome(found,"PREPARED","UI_REQUEST_PREPARED",false);diagnostics.Record("UI_REQUEST_PREPARED");}return true;},token).ConfigureAwait(false);
            bool receipt=await Task.Run(()=>channel.Report(found,Outcome(found,"PREPARED","UI_REQUEST_PREPARED",false),false),token).ConfigureAwait(false);
            if(!receipt)
                await scheduler.InvokeAsync(()=>{if(!disposed){prepared=null;claim=null;LastOutcome=Outcome(found,"REJECTED","UI_REQUEST_CLAIM_CHANGED",false);diagnostics.Record("UI_REQUEST_CLAIM_CHANGED_PREPARATION_DISCARDED");}return true;},token).ConfigureAwait(false);
        }
        catch(OperationCanceledException)
        {if((found??claim) is {} owned)try{await Task.Run(()=>channel.Report(owned,Outcome(owned,"REJECTED","UI_REQUEST_LIFETIME_CANCELLED",false),true)).ConfigureAwait(false);}catch{diagnostics.Record("UI_REQUEST_CANCEL_CLEANUP_RETAINED");}}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or PackageValidationException or InvalidOperationException)
        {diagnostics.Record("UI_REQUEST_IO_OR_CONTRACT_FAILURE_ISOLATED");
         // A failed PREPARED result write is not permission to select the candidate.
         // Clear managed preparation before releasing the poll/commit barrier.
         if(!disposed)await scheduler.InvokeAsync(()=>{prepared=null;return true;}).ConfigureAwait(false);
         if((found??claim) is {} owned)try{await EndAsync(owned,"REJECTED","UI_REQUEST_IO_OR_CONTRACT_FAILURE",false).ConfigureAwait(false);}catch{diagnostics.Record("UI_REQUEST_OWNED_CLEANUP_RETAINED");}}
        finally{if(!disposed)try{await scheduler.InvokeAsync(()=>{polling=false;return true;}).ConfigureAwait(false);}catch{diagnostics.Record("UI_REQUEST_DRIVER_COMPLETION_DEFERRED");}}
    }
    LocalUiRequestOutcome Outcome(ClaimedUiRequest c,string status,string reason,bool persisted)=>new(1,c.Request?.RequestId,c.Request?.CandidateContentId,coordinator.Selected?.Identity,status,reason,persisted);
    async Task EndAsync(ClaimedUiRequest c,string status,string reason,bool persisted)
    {
        var outcome=Outcome(c,status,reason,persisted);await Task.Run(()=>channel.Report(c,outcome,true)).ConfigureAwait(false);
        if(!disposed)await scheduler.InvokeAsync(()=>{LastOutcome=outcome;claim=null;prepared=null;diagnostics.Record(reason);return true;}).ConfigureAwait(false);
    }
    void RecordOutcome(string status,string reason,bool persisted,bool complete)
    {
        var c=claim!;var outcome=Outcome(c,status,reason,persisted);LastOutcome=outcome;diagnostics.Record(reason);
        work=Task.Run(()=>channel.Report(c,outcome,complete));
    }
    void Finish(string status,string reason,bool persisted)
    {
        var c=claim!;prepared=null;claim=null;polling=true;lastWait=null;var outcome=Outcome(c,status,reason,persisted);LastOutcome=outcome;diagnostics.Record(reason);
        work=FinishAsync(c,outcome);
    }
    async Task FinishAsync(ClaimedUiRequest c,LocalUiRequestOutcome outcome)
    {
        try {await Task.Run(()=>channel.Report(c,outcome,true)).ConfigureAwait(false);}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){diagnostics.Record("UI_REQUEST_RESULT_WRITE_FAILED_OWNED_CLAIM_RETAINED");}
        finally{if(!disposed)await scheduler.InvokeAsync(()=>{polling=false;return true;}).ConfigureAwait(false);}
    }
    public void Dispose(){if(disposed)return;disposed=true;cancellation.Cancel();prepared=null;if(claim is {} owned){var outcome=Outcome(owned,"REJECTED","UI_REQUEST_LIFETIME_CANCELLED",false);LastOutcome=outcome;work=Task.Run(()=>{try{return channel.Report(owned,outcome,true);}catch{diagnostics.Record("UI_REQUEST_CANCEL_CLEANUP_RETAINED");return false;}});claim=null;}}
}
