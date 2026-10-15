using System.Text.Json;
using GCMod;
using GCMod.Candidate;
using GCMod.Nextgen;
using GCMod.Services;

if (args.Length != 2) throw new ArgumentException("fixtures report");
using var fixtures = JsonDocument.Parse(await File.ReadAllBytesAsync(args[0]));
var tests = new List<object>();
foreach (var item in fixtures.RootElement.GetProperty("cases").EnumerateArray())
{
    var name = item.GetProperty("case").GetString()!;
    try
    {
        var root = item.GetProperty("root").GetString()!;
        var snapshot = await new PackageLoader(AdapterRegistry.CreateNg01()).LoadAsync(root);
        var store = new LocalPackageTranslationStore(snapshot);
        var manifest = await store.FetchManifestAsync();
        if (manifest == null || manifest.Novels.Count != item.GetProperty("registered_count").GetInt32()) throw new Exception("registration changed");
        using var manifestJson = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(root,"manifest.json")));
        var withoutHash = JsonSerializer.Serialize(manifestJson.RootElement.EnumerateObject().Where(p=>p.Name!="hash").ToDictionary(p=>p.Name,p=>p.Value));
        if (TranslationHash.Compute(withoutHash)!=manifest.Hash) throw new Exception("root semantic hash stale");
        var file = item.GetProperty("file").GetString()!;
        Dictionary<string, Dictionary<string, Dictionary<string,string>>>? master = null;
        Dictionary<string,string>? table = null;
        if (name=="master") master = await store.LoadAsync<Dictionary<string,Dictionary<string,Dictionary<string,string>>>>(TranslationPaths.Master);
        else if (name=="names") table=await store.LoadAsync<Dictionary<string,string>>(TranslationPaths.Names);
        else table=await store.LoadAsync<Dictionary<string,string>>(TranslationPaths.Novels,Path.GetFileNameWithoutExtension(file));
        var keys=item.GetProperty("keys").EnumerateArray().Select(x=>x.GetString()!).ToArray();
        var value=master!=null?master[keys[0]][keys[1]][keys[2]]:table?[keys[0]];
        if(value!=item.GetProperty("new_ko").GetString())throw new Exception("actual local store correction not returned");
        if(name=="unregistered" && manifest.Novels.ContainsKey(Path.GetFileNameWithoutExtension(file)))throw new Exception("automatic registration");
        tests.Add(new{id=name=="master"?"O03":name=="names"?"O04":name=="registered"?"O05":"O06",name="actual_candidate_store_"+name,status="PASS"});
    }
    catch(Exception error){tests.Add(new{name,status="FAIL",reason=error.ToString()});}
}
var options=new JsonSerializerOptions{WriteIndented=true};
var failed=tests.Count(x=>JsonSerializer.Serialize(x).Contains("\"FAIL\""));
var report=new{schema="gc-ng03-actual-semantic-store-v1",status=failed==0?"PASS":"FAIL",count=tests.Count,tests,
    assembly_sha256=PackageLoader.Hash(await File.ReadAllBytesAsync(typeof(LocalPackageTranslationStore).Assembly.Location)),
    game_interop_assembly_loaded=AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="Assembly-CSharp"),native_hook_install="NOT_RUN",game_validation="NOT_RUN"};
await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(report,options)+"\n");
Console.WriteLine($"ACTUAL SEMANTIC STORE {(failed==0?"PASS":"FAIL")} {tests.Count-failed}/{tests.Count}");
return failed==0?0:1;
