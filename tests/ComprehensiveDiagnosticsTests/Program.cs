using GCMod.Nextgen;
using System.Security.Cryptography;
using System.Text.Json;

var cases = new List<string>();
void Check(string name, bool value) { if (!value) throw new Exception(name); cases.Add(name); }
string owner = "Gc.Mission.MissionPopup", member = "OnChangeTab";
Check("missing and ambiguous separate", HookDiagnosticCodes.Discovery(owner,member,1,0).EndsWith("MISSING") && HookDiagnosticCodes.Discovery(owner,member,1,2).EndsWith("AMBIGUOUS"));
Check("unique discovery is not native availability", HookDiagnosticCodes.Discovery(owner,member,1,1).EndsWith("UNIQUE"));
var a = new[]{"System.Int32"}; var b = new[]{"System.String"};
Check("same arity different signature distinguishable", HookDiagnosticCodes.Identity(owner,member,a) != HookDiagnosticCodes.Identity(owner,member,b));
Check("same member different full owner distinguishable", HookDiagnosticCodes.Identity(owner,member,a) != HookDiagnosticCodes.Identity("Other.Namespace.MissionPopup",member,a));
var lines = HookDiagnosticCodes.Signature(owner,member,a).Concat(new[]{HookDiagnosticCodes.Outcome(owner,member,a,"UNAVAILABLE","NATIVE_METHOD_MAP_NO_ENTRY"),HookDiagnosticCodes.Discovery(owner,member,1,0)}).ToArray();
Check("bounded diagnostics accepted by actual sink", lines.All(x=>x.Length<=96));
var log = new SafeDiagnostics();foreach(string line in lines)log.Record(line);
Check("exact request role and argument recorded", log.Counters.Count==lines.Length && !log.Counters.ContainsKey("INVALID_DIAGNOSTIC_CODE") && lines.Any(x=>x.EndsWith("SYSTEM_INT32")));
Check("unavailable reason retained", lines.Any(x=>x.EndsWith("NATIVE_METHOD_MAP_NO_ENTRY")));
Check("parameter text cannot inject paths/control", HookDiagnosticCodes.Signature("A\n/B", "M", new[]{"T\r\n/<raw>"}).All(x=>x.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_')));
Console.WriteLine(JsonSerializer.Serialize(new{scope="ACTUAL_FINAL_CANDIDATE_DIAGNOSTIC_CPU",status="PASS",count=cases.Count,cases,
    assembly_sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(HookDiagnosticCodes).Assembly.Location))).ToLowerInvariant(),
    native_calls=0,native="NOT_RUN",game="NOT_RUN"},new JsonSerializerOptions{WriteIndented=true}));
