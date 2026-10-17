using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GCMod.Nextgen;

string package = Path.GetFullPath(args.Length == 1 ? args[0] : "translations/ko");
var results = new List<object>(); int failed = 0;
void Test(string name, Action body) { try { body(); results.Add(new { name, status = "PASS" }); } catch (Exception e) { failed++; results.Add(new { name, status = "FAIL", reason = e.Message }); } }
void Need(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
var donor = new FontDonorHandle(FontService.ApprovedProfile, 11, 17, 1);
var service = new FontService(); service.PublishMediumDonor(donor, new HangulOnly());
GeneralUiFontResult Probe(string output, bool access = true, FontDonorHandle? expected = null,
    Func<FontDonorHandle?>? current = null, Func<bool>? target = null, Func<bool>? rich = null,
    Func<int, bool>? medium = null, Func<string, bool>? primary = null)
    => BattleFontPolicy.Check(access, output, expected ?? donor, current ?? (() => donor), target ?? (() => true), rich ?? (() => true),
        medium ?? (s => service.Supports(new Rune(s).ToString(), donor)), primary ?? (_ => true));

Test("caption_role_keeps_space_and_punctuation_out_of_medium", () => {
    var scalars = new List<int>(); string? visible = null;
    var r = Probe("적 정보 / 전투 상세!", medium: s => { scalars.Add(s); return GeneralUiFontPolicy.RequiresMedium(s); }, primary: s => { visible = s; return s == "적 정보 / 전투 상세!"; });
    Need(r.Allowed && visible == "적 정보 / 전투 상세!" && !scalars.Contains(32) && !scalars.Contains('/'), "wrong_font_role");
    Need(!service.Supports("적 정보", donor), "global_donor_contract_was_relaxed");
});
Test("two_supported_formatting_examples_project_without_modifying_raw", () => {
    foreach (var pair in new[] {
        ("<color=#8d807a>또는</color> 오른쪽 <cspace=-0.1em>더블 클릭</cspace>", "또는 오른쪽 더블 클릭"),
        ("약점<cspace=-0.1em>원소</cspace>", "약점원소") }) {
        string raw = pair.Item1; byte[] before = Encoding.UTF8.GetBytes(raw); string? targetText = null;
        var r = Probe(raw, primary: value => { targetText = value; return value == pair.Item2; });
        Need(r.Allowed && targetText == pair.Item2 && before.SequenceEqual(Encoding.UTF8.GetBytes(raw)), "raw_tag_or_projection_changed");
    }
});
Test("required_hangul_missing_is_rejected", () => Need(Probe("필요 열정", medium: s => s != '열').Reason == "BATTLE_FONT_REQUIRED_MEDIUM_MISSING", "missing_hangul_admitted"));
Test("actual_primary_fallback_missing_space_is_rejected", () => Need(Probe("필요 열정", primary: s => !s.Contains(' ')).Reason == "BATTLE_FONT_PRIMARY_FALLBACK_MISSING", "missing_primary_admitted"));
Test("unknown_and_broken_glyph_tags_remain_rejected", () => {
    foreach (var text in new[] { "<sprite=1>가", "<font=Other>가</font>", "<br>가", "<b>가</b>", "<color=#ffffff>가", "<color=#ffffff><cspace=1>가</color></cspace>" })
        Need(Probe(text).Reason == "BATTLE_FONT_MARKUP_UNSUPPORTED", "unsupported_markup_admitted");
});
Test("richtext_off_checks_literal_angle_bytes", () => {
    string raw = "약점<cspace=-0.1em>원소</cspace>"; string? checkedText = null;
    Need(!Probe(raw, rich: () => false, primary: s => { checkedText = s; return !s.Contains('<'); }).Allowed && checkedText == raw, "literal_tag_was_hidden");
});
Test("offthread_blocks_all_target_richtext_and_glyph_reads", () => {
    int reads = 0; bool Read() { reads++; return true; }
    var r = Probe("필요 열정", access: false, current: () => { reads++; return donor; }, target: Read, rich: Read,
        medium: _ => Read(), primary: _ => Read());
    Need(r.Reason == "BATTLE_FONT_ACCESS_CLOSED" && reads == 0, "read_preceded_gate");
});
Test("stale_donor_blocks_target_and_coverage_reads", () => {
    int reads = 0; var r = Probe("적 정보", current: () => donor with { Generation = 2 }, target: () => { reads++; return true; },
        rich: () => { reads++; return true; }, medium: _ => { reads++; return true; }, primary: _ => { reads++; return true; });
    Need(r.Reason == "BATTLE_FONT_DONOR_STALE" && reads == 0, "stale_donor_native_reads");
});
Test("missing_unapproved_and_invalid_donors_remain_blocked", () => {
    foreach (var invalid in new FontDonorHandle?[] { null, donor with { Profile = "Other" }, donor with { NativeHandle = 0 }, donor with { InstanceId = 0 }, donor with { Generation = 0 } }) {
        int reads = 0; bool Read() { reads++; return true; }
        var r = BattleFontPolicy.Check(true, "적 정보", invalid, () => invalid, Read, Read, _ => Read(), _ => Read());
        Need(r.Reason == "BATTLE_FONT_DONOR_STALE" && reads == 0, "invalid_donor_admitted");
    }
});
Test("wrong_owner_lifetime_blocks_font_reads", () => {
    int reads = 0; var r = Probe("적 정보", target: () => false, rich: () => { reads++; return true; }, medium: _ => { reads++; return true; }, primary: _ => { reads++; return true; });
    Need(r.Reason == "BATTLE_FONT_TARGET_NOT_CURRENT" && reads == 0, "wrong_owner_font_reads");
});
Test("owner_reuse_during_primary_check_blocks_write_eligibility", () => {
    bool live = true; var r = Probe("적 정보", target: () => live, primary: _ => { live = false; return true; });
    Need(r.Reason == "BATTLE_FONT_TARGET_NOT_CURRENT", "reused_owner_admitted");
});
Test("donor_changes_during_coverage_remain_rejected", () => {
    var current = donor; var r = Probe("적 정보", current: () => current, primary: _ => { current = donor with { Generation = 2 }; return true; });
    Need(r.Reason == "BATTLE_FONT_DONOR_STALE", "changed_generation_admitted");
});
Test("target_read_exception_is_finite_unavailable", () => Need(Probe("적 정보", target: () => throw new IOException("explicit_mock")).Reason == "BATTLE_FONT_READ_UNAVAILABLE", "native_read_failure_not_isolated"));

using var resourceDoc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(package, "ui/resources.json")));
using var bindingDoc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(package, "ui/bindings.json")));
var resources = resourceDoc.RootElement.GetProperty("resources").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetString()!, x => x.Clone());
var battle = bindingDoc.RootElement.GetProperty("bindings").EnumerateArray().Where(x => x.GetProperty("enabled").GetBoolean() &&
    x.GetProperty("adapter_id").GetString() is "battle.caption.v1" or "battle.command-cost-title.v1").Select(x => x.Clone()).ToArray();
int captionCount = battle.Count(x => x.GetProperty("adapter_id").GetString() == "battle.caption.v1");
int commandCount = battle.Length - captionCount;
int spaces = 0, markup = 0;
Test("complete_current_battle_binding_corpus_uses_role_policy", () => {
    Need(captionCount > 0 && commandCount > 0, "one_battle_consumer_missing");
    foreach (var b in battle) {
        var r = resources[b.GetProperty("resource_id").GetString()!]; string ko = r.GetProperty("ko").GetString()!;
        if (ko.Contains(' ')) spaces++; if (ko.Contains('<')) markup++;
        // Independent expected projection of the actual current supported Battle corpus.
        string visible = Regex.Replace(ko, "</?(?:color(?:=#[0-9A-Fa-f]{6,8})?|cspace(?:=-?[0-9]+(?:\\.[0-9]+)?(?:em|px|%)?)?)>", "");
        string? actual = null;
        var result = Probe(ko, primary: text => { actual = text; return text == visible; });
        Need(result.Allowed && actual == visible, "corpus_font_failed_" + b.GetProperty("id").GetString());
    }
});
Test("completed_command_suffix_is_byte_exact_and_numeric_is_not_recomputed", () => {
    var label = new UiResource("fixture:completed-label", "battle.caption", "template_label", "必要熱情", "필요 열정", "translate", null);
    foreach (string suffix in new[] { " 12", "\r\n <color=#8d807a>009</color>  ", "<cspace=-0.1em> 7 </cspace>\r\n", " １２ / +003%\r\n" }) {
        string completed = label.Jp + suffix;
        Need(BattleCaptionPolicy.TryCostTitle(completed, label, MasterTranslationView.Empty, out string output), "completed_param_rejected");
        Need(output == label.Ko + suffix && Encoding.UTF8.GetBytes(output[label.Ko!.Length..]).SequenceEqual(Encoding.UTF8.GetBytes(suffix)) && completed == label.Jp + suffix, "native_suffix_changed");
        Need(Probe(output).Allowed, "valid_completed_output_font_rejected");
    }
});
Test("invalid_cost_parameter_is_not_invented", () => {
    var label = new UiResource("fixture:completed-label", "battle.caption", "template_label", "必要熱情", "필요 열정", "translate", null);
    foreach (string source in new[] { "必要熱情", "必要熱情abc12", "別の必要熱情 12", "必要熱情 なし" })
        Need(!BattleCaptionPolicy.TryCostTitle(source, label, MasterTranslationView.Empty, out _), "invalid_cost_param_accepted");
});
Test("cost_suffix_unknown_tag_is_preserved_but_font_denied", () => {
    var label = new UiResource("fixture:completed-label", "battle.caption", "template_label", "必要熱情", "필요 열정", "translate", null);
    string suffix = "<sprite=2> 12";
    Need(BattleCaptionPolicy.TryCostTitle(label.Jp + suffix, label, MasterTranslationView.Empty, out var output) && output == label.Ko + suffix &&
        Probe(output).Reason == "BATTLE_FONT_MARKUP_UNSUPPORTED", "suffix_was_rewritten_or_unsafe_tag_enabled");
});
Test("duplicate_group_names_join_only_current_typed_race_ancestry", () => {
    Need(BattleCaptionTargetPolicy.UniqueTypedAncestor(new long[]{31,32},new long[]{40,32,20})==32,"typed_race_ancestor_not_selected");
    Need(BattleCaptionTargetPolicy.UniqueTypedAncestor(new long[]{32},new long[]{40,32,20})==32,"original_unique_typed_group_rejected");
});
Test("missing_ambiguous_or_reused_race_ancestor_is_blocked", () => {
    foreach(var (groups,ancestors) in new[]{(new long[]{31,32},new long[]{40,20}),(new long[]{31,32},new long[]{32,31,20}),
        (new long[]{32,32},new long[]{40,32,20}),(new long[]{0,32},new long[]{40,32,20}),(new long[]{32},new long[]{40,32,32}),
        (Array.Empty<long>(),new long[]{32}),(new long[]{32},Array.Empty<long>())})
        Need(BattleCaptionTargetPolicy.UniqueTypedAncestor(groups,ancestors)==0,"unproven_group_selected");
});
Test("real_compiled_caption_and_card_calls_reach_same_font_policy", () => {
    using var pe = new PEReader(File.OpenRead(typeof(BattleFontPolicy).Assembly.Location)); var md = pe.GetMetadataReader();
    var type = md.TypeDefinitions.Single(h => md.GetString(md.GetTypeDefinition(h).Name) == "BattleNativeProvider");
    var methods = md.GetTypeDefinition(type).GetMethods().Where(h => md.GetString(md.GetMethodDefinition(h).Name) is "Apply" or "TryApplyCard" or "TryBattleFont")
        .ToDictionary(h => md.GetString(md.GetMethodDefinition(h).Name), h => h);
    string[] Calls(string method) => Tokens(pe.GetMethodBody(md.GetMethodDefinition(methods[method]).RelativeVirtualAddress).GetILBytes()!).Select(token => {
        var handle = MetadataTokens.EntityHandle(token);
        return handle.Kind switch { HandleKind.MethodDefinition => md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)handle).Name),
            HandleKind.MemberReference => md.GetString(md.GetMemberReference((MemberReferenceHandle)handle).Name), _ => "" };
    }).ToArray();
    Need(Calls("Apply").Contains("TryBattleFont") && Calls("TryApplyCard").Contains("TryBattleFont") && Calls("TryBattleFont").Contains("Check"), "actual_native_provider_not_wired");
    Need(!Calls("Apply").Contains("Supports") && !Calls("TryApplyCard").Contains("Supports"), "old_whole_string_medium_gate_remains");
});

Console.WriteLine(JsonSerializer.Serialize(new {
    status = failed == 0 ? "PASS" : "FAIL", count = results.Count, failures = failed,
    scope = "ACTUAL_CANDIDATE_CPU_POLICY_AND_COMPILED_PROVIDER_CALLS_WITH_EXPLICIT_MOCK_FONT_INPUTS_NOT_NATIVE_PROOF",
    candidate_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(BattleFontPolicy).Assembly.Location))).ToLowerInvariant(),
    corpus = new { captions = captionCount, command_costs = commandCount, spaces, markup },
    native_hook_installation = "NOT_RUN", native_glyph_material_geometry = "NOT_RUN", results
}, new JsonSerializerOptions { WriteIndented = true }));
return failed == 0 ? 0 : 1;

static IEnumerable<int> Tokens(byte[] bytes) {
    var ops = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(x => x.FieldType == typeof(OpCode)).Select(x => (OpCode)x.GetValue(null)!).ToDictionary(x => unchecked((ushort)x.Value));
    for (int i = 0; i < bytes.Length;) {
        ushort code = bytes[i++]; if (code == 0xfe) code = (ushort)(0xfe00 | bytes[i++]); var op = ops[code];
        if (op.OperandType == OperandType.InlineMethod) yield return BitConverter.ToInt32(bytes, i);
        i += op.OperandType switch { OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, i), _ => 4 };
    }
}
sealed class HangulOnly : IFontGlyphProvider { public bool HasGlyph(FontDonorHandle donor, int scalar) => GeneralUiFontPolicy.RequiresMedium(scalar); }
