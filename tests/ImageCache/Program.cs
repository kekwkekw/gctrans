using GCMod.Services;
using System.Net;
using System.Text;
using System.Text.Json;

string source=Path.GetFullPath(args[0]);
string json=File.ReadAllText(Path.Combine(source,"images.json"));
var catalog=ImageCatalog.Parse(json);
string temp=Path.Combine(Path.GetTempPath(),"gcmod-image-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
var handler=new MockHttp(source);using var client=new HttpClient(handler);
try
{
    await ImageCache.PrepareAsync(client,"https://example.test/translations/ko",temp,ImageCatalog.Hash(json),default);
    if(File.ReadAllText(Path.Combine(temp,"images.json"))!=json)throw new Exception("Catalog publication failed");
    foreach(var e in catalog.Entries)if(!e.Matches(File.ReadAllBytes(Path.Combine(temp,e.File))))throw new Exception("Wrong PNG");
    handler.Calls=0;
    await ImageCache.PrepareAsync(client,"https://example.test/translations/ko",temp,ImageCatalog.Hash(json),default);
    if(handler.Calls!=0)throw new Exception("Unchanged payload downloaded again");

    // Valid replacement mapping but corrupt remote PNG: previous catalog survives.
    var modified=ImageCatalog.Parse(json);modified.Entries[0].Sha256=new string('a',64);
    modified.Entries[0].File="images/"+modified.Entries[0].Sha256+".png";
    string next=JsonSerializer.Serialize(modified);handler.Catalog=next;handler.Corrupt=true;
    await ImageCache.PrepareAsync(client,"https://example.test/translations/ko",temp,ImageCatalog.Hash(next),default);
    if(File.ReadAllText(Path.Combine(temp,"images.json"))!=json)throw new Exception("Incomplete catalog published");

    // Deleting a mapping is an explicit data update; stale cached PNGs do not remain selected.
    string empty="{\"schema\":\"gc-images-v1\",\"entries\":[]}";handler.Catalog=empty;handler.Corrupt=false;
    await ImageCache.PrepareAsync(client,"https://example.test/translations/ko",temp,ImageCatalog.Hash(empty),default);
    if(ImageCatalog.Parse(File.ReadAllText(Path.Combine(temp,"images.json"))).Entries.Count!=0)throw new Exception("Mapping removal failed");

    // A broken local catalog can recover from the pinned remote catalog.
    File.WriteAllText(Path.Combine(temp,"images.json"),"{");handler.Catalog=json;
    await ImageCache.PrepareAsync(client,"https://example.test/translations/ko",temp,ImageCatalog.Hash(json),default);
    if(File.ReadAllText(Path.Combine(temp,"images.json"))!=json)throw new Exception("Corrupt cache recovery failed");
    using var cancellation=new CancellationTokenSource();cancellation.Cancel();
    try {await ImageCache.PrepareAsync(client,"https://example.test/translations/ko",temp,ImageCatalog.Hash(empty),cancellation.Token);throw new Exception("Cancellation ignored");}
    catch(OperationCanceledException){}
    if(File.ReadAllText(Path.Combine(temp,"images.json"))!=json)throw new Exception("Cancelled update changed catalog");

    foreach(var invalid in new[]{json.Replace("gc-images-v1","gc-images-v999"),json.Replace("images/","../"),json.Replace("\"sprite\"","\"code\"")})
    {try{ImageCatalog.Parse(invalid);throw new Exception("Invalid catalog accepted");}catch(FormatException){}}
    var duplicate=ImageCatalog.Parse(json);duplicate.Entries.Add(duplicate.Entries[0]);
    try{ImageCatalog.Parse(JsonSerializer.Serialize(duplicate));throw new Exception("Duplicate target accepted");}catch(FormatException){}
    // Multiple targets may share a PNG, but every target's dimensions must be checked.
    var shared=ImageCatalog.Parse(json);
    var wrong=JsonSerializer.Deserialize<ImageEntry>(JsonSerializer.Serialize(shared.Entries[0]));
    wrong.Name="shared-wrong-dimensions";wrong.Width++;shared.Entries.Add(wrong);
    handler.Catalog=JsonSerializer.Serialize(shared);
    await ImageCache.PrepareAsync(client,"https://example.test/translations/ko",temp,ImageCatalog.Hash(handler.Catalog),default);
    if(File.ReadAllText(Path.Combine(temp,"images.json"))!=json)throw new Exception("Invalid shared target published");

    // A complete recollection set exceeds the old 128-target cap. Simulate
    // ordinary latency and verify bounded parallelism, deduplication and cancellation.
    string bulkRoot=Path.Combine(temp,"bulk");Directory.CreateDirectory(bulkRoot);
    var bulk=new ImageCatalog{Schema="gc-images-v1",Entries=new()};
    byte[] seed=File.ReadAllBytes(Path.Combine(source,catalog.Entries[0].File));
    var payloads=new Dictionary<string,byte[]>();
    for(int i=0;i<156;i++)
    {
        byte[] raw=seed.Concat(BitConverter.GetBytes(i)).ToArray();
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(raw)).ToLowerInvariant();
        string file="images/"+hash+".png";payloads[file]=raw;
        bulk.Entries.Add(new ImageEntry{Kind="texture",Name="bulk-"+i,Width=catalog.Entries[0].Width,Height=catalog.Entries[0].Height,File=file,Sha256=hash,Bytes=raw.Length});
    }
    string bulkJson=JsonSerializer.Serialize(bulk);ImageCatalog.Parse(bulkJson);
    using var bulkHttp=new BulkHttp(bulkJson,payloads);using var bulkClient=new HttpClient(bulkHttp);
    using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        await ImageCache.PrepareAsync(bulkClient,"https://example.test/translations/ko",bulkRoot,ImageCatalog.Hash(bulkJson),deadline.Token);
    if(File.ReadAllText(Path.Combine(bulkRoot,"images.json"))!=bulkJson)throw new Exception("Full catalog publication failed");
    if(bulkHttp.Peak<2||bulkHttp.Peak>4)throw new Exception("Download concurrency is not bounded to four");
    foreach(var e in bulk.Entries)if(!e.Matches(File.ReadAllBytes(Path.Combine(bulkRoot,e.File))))throw new Exception("Bulk payload mismatch");
    // Shared mapping should reuse the same payload without a second download.
    var same=JsonSerializer.Deserialize<ImageEntry>(JsonSerializer.Serialize(bulk.Entries[0]));same.Name="shared-valid";bulk.Entries.Add(same);
    string sharedJson=JsonSerializer.Serialize(bulk);bulkHttp.Catalog=sharedJson;bulkHttp.Calls=0;
    await ImageCache.PrepareAsync(bulkClient,"https://example.test/translations/ko",bulkRoot,ImageCatalog.Hash(sharedJson),default);
    if(bulkHttp.Calls!=1)throw new Exception("Shared payload fetched again");
    // Cancellation during active downloads must not publish partial metadata.
    string cancelledRoot=Path.Combine(temp,"cancelled");Directory.CreateDirectory(cancelledRoot);File.WriteAllText(Path.Combine(cancelledRoot,"images.json"),empty);
    using(var deadline=new CancellationTokenSource(TimeSpan.FromMilliseconds(350)))
    {
        try{await ImageCache.PrepareAsync(bulkClient,"https://example.test/translations/ko",cancelledRoot,ImageCatalog.Hash(sharedJson),deadline.Token);throw new Exception("Active cancellation ignored");}
        catch(OperationCanceledException){}
    }
    if(File.ReadAllText(Path.Combine(cancelledRoot,"images.json"))!=empty)throw new Exception("Cancelled bulk catalog published");
    if(Directory.EnumerateFiles(cancelledRoot,"*.tmp",SearchOption.AllDirectories).Any())throw new Exception("Cancelled temporary files retained");
    while(bulk.Entries.Count<257){var e=JsonSerializer.Deserialize<ImageEntry>(JsonSerializer.Serialize(bulk.Entries[0]));e.Name="limit-"+bulk.Entries.Count;bulk.Entries.Add(e);}
    try{ImageCatalog.Parse(JsonSerializer.Serialize(bulk));throw new Exception("257 targets accepted");}catch(FormatException){}
    Console.WriteLine("PASS: 156-target cold update under 15-second budget, concurrency <=4, shared-target validation, active cancellation, 256-target cap");
    Console.WriteLine("PASS: image download, hashes, unchanged reuse, atomic failure, removal, recovery, cancellation, invalid target/path/schema");
}
finally {Directory.Delete(temp,true);}

sealed class MockHttp(string root):HttpMessageHandler
{
    public int Calls;public string Catalog;public bool Corrupt;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();Interlocked.Increment(ref Calls);
        string path=request.RequestUri!.AbsolutePath.Replace("/translations/ko/","");
        byte[] bytes=path=="images.json"?Encoding.UTF8.GetBytes(Catalog??File.ReadAllText(Path.Combine(root,path))):Corrupt?new byte[33]:File.ReadAllBytes(Path.Combine(root,path));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});
    }
}
sealed class BulkHttp(string catalog,Dictionary<string,byte[]> payloads):HttpMessageHandler
{
    public string Catalog=catalog;public int Calls;public int Peak;private int active;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Calls);
        string path=request.RequestUri!.AbsolutePath.Replace("/translations/ko/","");
        if(path=="images.json")return new(HttpStatusCode.OK){Content=new ByteArrayContent(Encoding.UTF8.GetBytes(Catalog))};
        int count=Interlocked.Increment(ref active);int old;
        do{old=Peak;}while(count>old&&Interlocked.CompareExchange(ref Peak,count,old)!=old);
        try{await Task.Delay(150,cancellationToken);return new(HttpStatusCode.OK){Content=new ByteArrayContent(payloads[path])};}
        finally{Interlocked.Decrement(ref active);}
    }
}
namespace GCMod.Services { public static class Logger {public static void Info(string message){} public static void Warn(string message){} } }
