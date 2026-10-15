using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using GCMod.Nextgen;

// Links the actual product library. It never starts Unity or invokes a native address.
var service = new CompatibilityService();
var signature = new NativeMethodContract("Fixture.Owner", "Setup", false, "System.Void",
    ImmutableArray<NativeParameter>.Empty, "IL2CPP_GENERATED_WRAPPER_V1");
var results = new List<object>();
void Check(string name, int count, bool shared, bool expectedAvailable, string expectedReason,
    bool executable = true, bool supportedLayout = true)
{
    var observation = new NativeMethodObservation(signature, 1, executable, supportedLayout,
        count, shared, signature.DeclaringType, signature.Abi);
    var result = service.Assess(signature, observation);
    if (result.Available != expectedAvailable || result.Reason != expectedReason)
        throw new InvalidOperationException($"{name}: got {result}; expected {expectedAvailable}/{expectedReason}");
    results.Add(new { name, status = "PASS", result.Available, result.Reason });
}
Check("missing-map-is-not-shared-proof", 0, false, false, "NATIVE_METHOD_MAP_NO_ENTRY");
Check("unique-map", 1, false, true, "VERIFIED_COMPATIBILITY_ONLY");
Check("two-members-remain-blocked", 2, true, false, "SHARED_OR_ALIASED_NATIVE_BODY");
Check("shared-flag-remains-blocked", 1, true, false, "SHARED_OR_ALIASED_NATIVE_BODY");
Check("invalid-count-remains-blocked", -1, false, false, "NATIVE_METHOD_MAP_COUNT_INVALID");
Check("executable-guard-retained", 1, false, false, "TARGET_NOT_CURRENT_EXECUTABLE", executable: false);
Check("layout-guard-retained", 1, false, false, "UNSUPPORTED_INTEROP_LAYOUT", supportedLayout: false);
foreach (var name in new[] { "Cast", "Box", "Unbox" })
{
    var method = typeof(InlineTypeFixture).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;
    var found = ManagedIlReader.StaticHandleFields(method, "NativeMethodInfoPtr_" + name + "_");
    if (found.Length != 1) throw new InvalidOperationException("IL handle resolution failed: " + name);
    results.Add(new { name = "InlineType-" + name, status = "PASS" });
}
Console.WriteLine(JsonSerializer.Serialize(new { scope = "ACTUAL_PRODUCT_CPU_ONLY", results,
    native_calls = 0, native = "NOT_RUN", game = "NOT_RUN" }, new JsonSerializerOptions { WriteIndented = true }));

static class InlineTypeFixture
{
    static readonly IntPtr NativeMethodInfoPtr_Cast_Public = new(1);
    static readonly IntPtr NativeMethodInfoPtr_Box_Public = new(2);
    static readonly IntPtr NativeMethodInfoPtr_Unbox_Public = new(3);
    public static IntPtr Cast(object value) { GC.KeepAlive((string)value); return NativeMethodInfoPtr_Cast_Public; }
    public static IntPtr Box(int value) { GC.KeepAlive(value); return NativeMethodInfoPtr_Box_Public; }
    public static IntPtr Unbox(object value) { GC.KeepAlive((int)value); return NativeMethodInfoPtr_Unbox_Public; }
}
