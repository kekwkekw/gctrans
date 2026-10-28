using System.Net;
using System.Text.Json;
using GCMod.Services;

// Read-only network probe against a caller-prepared scratch cache, never a game install.
if (args.Length == 3)
{
    string scratch = Path.GetFullPath(args[1]);
    if (!File.Exists(Path.Combine(scratch, "SCRATCH_CACHE_ONLY")))
        throw new Exception("Refusing probe without SCRATCH_CACHE_ONLY marker");
    using var network = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    var probe = new TranslationCache(args[0], scratch, "ko", network);
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await probe.PrepareStartupAsync(deadline.Token);
    var ui = await probe.LoadAsync("ui", cancellationToken: deadline.Token);
    string actual = TranslationHash.Compute(File.ReadAllText(Path.Combine(scratch,"ko","ui.json")));
    bool passed = probe.Manifest?.Ui == actual && ui.TryGetValue("第{0}章", out var chapter) && chapter=="제{0}장";
    var report = new { status=passed?"PASS":"FAIL", source=args[0], ui_hash=actual,
        manifest_ui=probe.Manifest?.Ui, ui_pairs=ui.Count, native="NOT_RUN", game_install_changed=false };
    File.WriteAllText(args[2],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine(JsonSerializer.Serialize(report));
    if(!passed) throw new Exception("Remote UI update was not observed");
    return;
}

string root = Path.Combine(Path.GetTempPath(), "gc-cache-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "ko", "novels"));
void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
var handler = new Origin();
using var client = new HttpClient(handler);
string Json(object x) => JsonSerializer.Serialize(x);
Dictionary<string, string> Revision(string suffix)
{
    var files = new Dictionary<string, string> {
        ["names"] = Json(new Dictionary<string,string>{{"館長","관장"+suffix}}),
        ["ui"] = Json(new Dictionary<string,string>{{"第{0}章","제{0}장"+suffix}}),
        ["master"] = Json(new { mItems = new { name = new Dictionary<string,string>{{"日本語","한국어"+suffix}} } }),
        ["novels/1"] = Json(new Dictionary<string,string>{{"本文","본문"+suffix}})
    };
    var manifest = new Dictionary<string,object> {
        ["names"] = TranslationHash.Compute(files["names"]),
        ["master"] = TranslationHash.Compute(files["master"]),
        ["ui"] = TranslationHash.Compute(files["ui"]),
        ["novels"] = new Dictionary<string,string>{{"1", TranslationHash.Compute(files["novels/1"])}}
    };
    manifest["hash"] = TranslationHash.Compute(Json(manifest));
    files["manifest"] = Json(manifest);
    return files;
}
TranslationCache Cache(string cdn="https://example.invalid/main") => new(cdn,root,"ko",client);
async Task<string> Ui(TranslationCache c) => (await c.LoadAsync("ui"))["第{0}章"];
var v1=Revision("1"); var v2=Revision("2");
try
{
    foreach(var p in v1) File.WriteAllText(Path.Combine(root,"ko",p.Key+".json"),p.Value);
    handler.Files=v2;
    var cache=Cache();
    await cache.PrepareStartupAsync(default);
    Check(await Ui(cache)=="제{0}장2", "UI existing cache must update");
    Check((await cache.LoadAsync("names"))["館長"]=="관장2", "Names update");
    Check(File.ReadAllText(Path.Combine(root,"ko","master.json"))==v2["master"], "Master update before hook");
    Check(!handler.Requests.Contains("novels/1"), "Novel must remain lazy");
    Check((await cache.LoadAsync("novels","1"))["本文"]=="본문2", "Lazy Novel update");
    handler.Requests.Clear();
    await Cache().PrepareStartupAsync(default);
    Check(handler.Requests.SequenceEqual(new[]{"manifest"}), "Unchanged payloads must not redownload");
    handler.Requests.Clear();
    await Cache("offline").PrepareStartupAsync(default);
    Check(handler.Requests.Count==0, "Offline must perform no requests");

    var bad=Revision("3"); bad["ui"]=Json(new { broken="corrupt hash" });
    handler.Files=bad;
    cache=Cache();await cache.PrepareStartupAsync(default);
    Check(await Ui(cache)=="제{0}장2", "Hash mismatch must keep good UI");
    Check(File.ReadAllText(Path.Combine(root,"ko","ui.json"))==v2["ui"], "Never persist mismatching payload");
    handler.Files=Revision("4");handler.Files["ui"]="{";
    cache=Cache();await cache.PrepareStartupAsync(default);
    Check(await Ui(cache)=="제{0}장2","Malformed JSON must keep good UI");
    handler.Files=Revision("5"); handler.Files["manifest"]="{\"names\":\"invalid\"}";
    cache=Cache();await cache.PrepareStartupAsync(default);
    Check(await Ui(cache)=="제{0}장2","Malformed manifest must not admit unverified UI");
    handler.Fail=true;
    cache=Cache();await cache.PrepareStartupAsync(default);
    Check(await Ui(cache)=="제{0}장2","Network outage fallback");
    handler.Fail=false;handler.Delay=true;
    using(var timeout=new CancellationTokenSource(TimeSpan.FromMilliseconds(30)))
    {
        try { await Cache().PrepareStartupAsync(timeout.Token); throw new Exception("Cancellation ignored"); }
        catch(OperationCanceledException) { }
    }
    Check(File.ReadAllText(Path.Combine(root,"ko","ui.json"))==v2["ui"],"Timeout preserved cache");
    Check(!Directory.EnumerateFiles(root,"*.tmp",SearchOption.AllDirectories).Any(),"Temporary files cleaned");
    Console.WriteLine("PASS: production cache; revision changes, Master/UI/Names, lazy Novel, offline, no-op, bad hash/JSON/manifest, outage, timeout.");
}
finally { Directory.Delete(root,true); }

sealed class Origin : HttpMessageHandler
{
    public Dictionary<string,string> Files=new();
    public List<string> Requests=new();
    public bool Fail,Delay;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    {
        string path=request.RequestUri.AbsolutePath.Split("/translations/ko/")[1][..^5];
        lock(Requests) Requests.Add(path);
        if(Delay) await Task.Delay(10000,token);
        if(Fail) throw new HttpRequestException("simulated outage");
        return Files.TryGetValue(path,out var json)
            ? new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)}
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
