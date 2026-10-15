using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

if(args.Length!=3)throw new ArgumentException("CompileAudit <MSBuild Compile items.json> <candidate.dll> <report.json>");
using var items=JsonDocument.Parse(await File.ReadAllBytesAsync(args[0]));
var sources=new List<string>();var violations=new List<string>();
var forbidden=new Regex(@"\b(?:InitAutoLease|AutoTick|compiledDataSha|HostOnlyProof|ValidateLocalPackageOnLoad)\b",RegexOptions.CultureInvariant);
foreach(var item in items.RootElement.GetProperty("Items").GetProperty("Compile").EnumerateArray())
{
    string path=item.GetProperty("FullPath").GetString()!;
    if(path.Replace('\\','/').Contains("/obj/",StringComparison.Ordinal))continue;
    string identity=item.GetProperty("Identity").GetString()!.Replace('\\','/');sources.Add(identity);
    string text=await File.ReadAllTextAsync(path);
    if(path.Replace('\\','/').Contains("/tests/",StringComparison.Ordinal))violations.Add("test_source_compiled_into_product:"+identity);
    if(Path.GetFileName(path).Contains("AutoLease",StringComparison.Ordinal)||Path.GetFileName(path)=="NextgenPackageBootstrap.cs")violations.Add("excluded_source_compiled:"+identity);
    if(forbidden.IsMatch(text))violations.Add("forbidden_operational_path:"+identity);
    if(path.Replace('\\','/').Contains("/Native/",StringComparison.Ordinal)&&Regex.IsMatch(text,@"\b(?:MemoryTextTarget|HostOnly|allow_unknown_build)\b"))violations.Add("fixture_or_unsafe_native_provider:"+identity);
}
var bytes=await File.ReadAllBytesAsync(args[1]);
using var pe=new PEReader(new MemoryStream(bytes));
var metadata=pe.GetMetadataReader();var types=new HashSet<string>(StringComparer.Ordinal);
foreach(var handle in metadata.TypeDefinitions)
{
    var type=metadata.GetTypeDefinition(handle);string ns=metadata.GetString(type.Namespace),name=metadata.GetString(type.Name);
    types.Add(ns.Length==0?name:ns+"."+name);
}
string[] required=["GCMod.Plugin","GCMod.Candidate.NativeRuntimeBridge","GCMod.Nextgen.RuntimeCoordinator",
    "GCMod.Candidate.LegacyTranslationRuntime","GCMod.Candidate.Native.NativeHookHub","GCMod.Candidate.Native.GeneralUiProvider",
    "GCMod.Candidate.Native.StoryNativeProvider","GCMod.Candidate.Native.BattleNativeProvider","GCMod.Candidate.Native.Fit3NativeProvider",
    "GCMod.Nextgen.FixedRefDataUpdater","GCMod.Nextgen.ReleaseDescriptor","GCMod.Nextgen.ChoiceProofMetrics",
    "GCMod.Nextgen.LocalImmutablePackageStore","GCMod.Nextgen.ManagedSettingsMailbox",
    "GCMod.Nextgen.LegacyPayloadContracts","GCMod.Nextgen.LegacySemanticHash"];
foreach(var name in required)if(!types.Contains(name))violations.Add("required_product_type_missing:"+name);
if(types.Any(x=>x.Contains("AutoLease",StringComparison.Ordinal)))violations.Add("AutoLease_type_compiled");
var output=Path.GetFullPath(args[2]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var report=new{schema="gc-ng03-product-compile-metadata-audit-v1",status=violations.Count==0?"PASS":"FAIL",candidate_sha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
    source_items=sources.Count,required_types=required,inspected_source_items=sources.Order(StringComparer.Ordinal),violations,
    inspection="MSBuild actual Compile items and direct PE metadata reader; assembly not loaded",native_hook_install="NOT_RUN",game_validation="NOT_RUN",publication="NOT_PERFORMED",
    original_game_DLL_or_font_execution=0,
    native_fixture_token_matches=violations.Count(x=>x.StartsWith("fixture_or_unsafe_native_provider:",StringComparison.Ordinal)),
    test_source_items=violations.Count(x=>x.StartsWith("test_source_compiled_into_product:",StringComparison.Ordinal)),
    audit_limit="Required product/native types and bounded source exclusion/token checks; native execution and ABI compatibility are not assessed"};
await File.WriteAllTextAsync(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})+"\n");
Console.WriteLine($"COMPILE METADATA {(violations.Count==0?"PASS":"FAIL")}: {sources.Count} product sources, {required.Length} required types");
return violations.Count==0?0:1;
