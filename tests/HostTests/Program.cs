using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using GCMod.Nextgen;

namespace HostTests;

internal static partial class Program
{
    static readonly List<object> Results = [];
    static int failures;
    static string Scratch = "";
    static async Task Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "HOST_TEST_RESULTS.json");
        Scratch = Path.Combine(Path.GetDirectoryName(output)!, "scratch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Scratch);
        TestCompatibility();
        await TestPackageAndModules(args.Length > 1 ? Path.GetFullPath(args[1]) : null, args.Length > 2 ? Path.GetFullPath(args[2]) : null);
        TestOwnership();
        var report = new { schema = "gc-ng01-host-test-v1", status = failures == 0 ? "PASS" : "FAIL",
            tests = Results, count = Results.Count, failures, game_runtime = "NOT_RUN_NO_GAME_AUTHORIZATION",
            historical_receipt_reads = 0, donor_assembly_execution = false,
            tested_assembly_sha256 = Hash(await File.ReadAllBytesAsync(typeof(PackageLoader).Assembly.Location)),
            execution_framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription };
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"HOST {(failures == 0 ? "PASS" : "FAIL")} {Results.Count - failures}/{Results.Count}; evidence={output}");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    static void Check(string name, Action action)
    {
        try { action(); Results.Add(new { name, status = "PASS" }); }
        catch (Exception ex) { failures++; Results.Add(new { name, status = "FAIL", error = ex.Message }); Console.WriteLine($"FAIL {name}: {ex.Message}"); }
    }
    static async Task CheckAsync(string name, Func<Task> action)
    {
        try { await action(); Results.Add(new { name, status = "PASS" }); }
        catch (Exception ex) { failures++; Results.Add(new { name, status = "FAIL", error = ex.Message }); Console.WriteLine($"FAIL {name}: {ex.Message}"); }
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static void TestCompatibility()
    {
        var contract = new NativeMethodContract("Gc.FilterPopup", "Setup", false, "System.Void",
            [new("System.Int32"), new("System.String", true)], "win64-il2cpp-v1");
        var observed = new NativeMethodObservation(contract, 1, true, true, 1, false, "Gc.FilterPopup", "win64-il2cpp-v1");
        var service = new CompatibilityService();
        Check("ABI_exact_signature_available", () => Assert(service.Assess(contract, observed).Available, "exact supported observation rejected"));
        var cases = new Dictionary<string, NativeMethodObservation> {
            ["return_type"] = observed with { Signature = contract with { ReturnType = "System.Int32" } },
            ["parameter_type"] = observed with { Signature = contract with { Parameters = [new("System.UInt32"), new("System.String", true)] } },
            ["parameter_order"] = observed with { Signature = contract with { Parameters = [new("System.String", true), new("System.Int32")] } },
            ["parameter_byref"] = observed with { Signature = contract with { Parameters = [new("System.Int32"), new("System.String")] } },
            ["static_instance"] = observed with { Signature = contract with { IsStatic = true } },
            ["declaring_class"] = observed with { NativeDeclaringClass = "Gc.OtherPopup" },
            ["generic_arity"] = observed with { Signature = contract with { GenericArity = 1 } },
            ["backing_field"] = observed with { Signature = contract with { BackingFieldType = "UnverifiedLayout" } },
            ["abi"] = observed with { Abi = "other-abi" },
            ["non_unique"] = observed with { CandidateCount = 2 },
            ["current_target"] = observed with { CurrentExecutableTarget = false },
            ["interop_layout"] = observed with { SupportedInteropLayout = false },
            ["alias"] = observed with { AliasCount = 2 },
            ["shared_body"] = observed with { SharedBody = true },
        };
        foreach (var (name, actual) in cases)
            Check("ABI_reject_" + name, () => Assert(!service.Assess(contract, actual).Available, "unsafe observation available"));
        Check("native_registry_default_disarmed", () => Assert(AdapterRegistry.CreateNg01().Descriptors.All(x => !x.Available), "unverified native capability armed"));
        Check("diagnostics_bounded_and_sink_failure_isolated", () => {
            var diagnostics = new SafeDiagnostics(3, _ => throw new IOException("fixture sink unavailable"));
            for (int i = 0; i < 20; i++) diagnostics.Record("FIXTURE_" + i);
            Assert(diagnostics.Recent.Length <= 3 && diagnostics.Counters.Count <= 3 && diagnostics.SinkFailures == 20, "logger failed or unbounded");
        });
    }
}
