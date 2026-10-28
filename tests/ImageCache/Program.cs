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
    Console.WriteLine("PASS: image download, hashes, unchanged reuse, atomic failure, removal, recovery, cancellation, invalid target/path/schema");
}
finally {Directory.Delete(temp,true);}

sealed class MockHttp(string root):HttpMessageHandler
{
    public int Calls;public string Catalog;public bool Corrupt;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();Calls++;
        string path=request.RequestUri!.AbsolutePath.Replace("/translations/ko/","");
        byte[] bytes=path=="images.json"?Encoding.UTF8.GetBytes(Catalog??File.ReadAllText(Path.Combine(root,path))):Corrupt?new byte[33]:File.ReadAllBytes(Path.Combine(root,path));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});
    }
}
namespace GCMod.Services { public static class Logger {public static void Info(string message){} public static void Warn(string message){} } }
