using GCMod.Nextgen;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

var tests=new List<object>();int failed=0;
void Need(bool value,string reason){if(!value)throw new InvalidDataException(reason);}
void Test(string name,Action test){try{test();tests.Add(new{name,status="PASS"});}catch(Exception e){failed++;tests.Add(new{name,status="FAIL",reason=e.Message});}}
var caption=new UiBinding("fixture.character.enhance","fixture.resource","ui.chrome.v1",true,new("current-model","[]","強化"),
    new("gc.character.charactertopcontroller","artist_enhance",ImmutableArray.Create("Animation_Main","Frame_Character","Unit","BtUnitEnhance","Text"),Type:"caption",Lifetime:"owner"),null);
Test("only_recovered_character_enhancement_caption_role_can_scope_layout",()=>{
    Need(CharacterEnhanceGeometryPolicy.Supports(caption),"accepted_exact_role_rejected");
    foreach(var b in new[]{caption with{Enabled=false},caption with{AdapterId="ui4.static-caption.v1"},
        caption with{SourceIdentity=caption.SourceIdentity with{Kind="static-asset"}},caption with{SourceIdentity=caption.SourceIdentity with{ExactJp="交流"}},
        caption with{Selector=caption.Selector with{ScopeKey="gc.item.itemtopcontroller"}},caption with{Selector=caption.Selector with{SlotKey="imaju_enhance"}},
        caption with{Selector=caption.Selector with{Type="static"}},caption with{Selector=caption.Selector with{Lifetime="cell"}}})
        Need(!CharacterEnhanceGeometryPolicy.Supports(b),"unrelated_role_lost_exact_geometry_contract");
});
Test("direct_native_button_path_is_exact_not_first_match_or_substring",()=>{
    Need(CharacterEnhanceGeometryPolicy.MatchesPath(caption.Selector.Segments),"exact_path_rejected");
    foreach(var path in new[]{new[]{"Animation_Main","Frame_Character","Unit","BtUnitPresent","Text"},
        new[]{"Animation_Main","Frame_Character","SubUnit","BtUnitEnhance","Text"},
        new[]{"Animation_Main","Frame_Character","Unit","BtUnitEnhance","Text_Extra"},
        new[]{"Animation_Main","Frame_Character","Unit","BtUnitEnhance"},new[]{"Text"},Array.Empty<string>()})
        Need(!CharacterEnhanceGeometryPolicy.MatchesPath(path),"different_physical_role_admitted");
});
Test("current_subscene_identity_refuses_unselected_missing_duplicate_or_oversize",()=>{
    Need(CharacterEnhanceGeometryPolicy.SelectedOwner(12,1,new long[]{11,12,13}),"actual_selected_identity_join_rejected");
    foreach(var (owner,index,scenes) in new[]{(0L,0,new long[]{0}),(12L,-1,new long[]{12}),(12L,1,new long[]{12}),
        (12L,0,new long[]{11,12}),(12L,0,new long[]{12,12}),(12L,0,Enumerable.Repeat(12L,33).ToArray()),(12L,0,Array.Empty<long>())})
        Need(!CharacterEnhanceGeometryPolicy.SelectedOwner(owner,index,scenes),"noncurrent_subscene_admitted");
});
Test("actual_candidate_typed_button_and_late_identity_guard_are_wired",()=>{
    using var pe=new PEReader(File.OpenRead(typeof(CharacterEnhanceGeometryPolicy).Assembly.Location));var md=pe.GetMetadataReader();
    var h=md.TypeDefinitions.Single(h=>md.GetString(md.GetTypeDefinition(h).Name)=="GeneralUiProvider");
    var methods=md.GetTypeDefinition(h).GetMethods().Where(h=>md.GetString(md.GetMethodDefinition(h).Name) is "CharacterEnhanceContainer" or "TryCharacterEnhanceGeometry" or "CurrentCharacterEnhanceGeometry" or "TryPrepareGeometry" or "GeometryLayout").ToDictionary(h=>md.GetString(md.GetMethodDefinition(h).Name));
    string[] Calls(string method)=>Tokens(pe.GetMethodBody(md.GetMethodDefinition(methods[method]).RelativeVirtualAddress).GetILBytes()!).Select(token=>{
        var x=MetadataTokens.EntityHandle(token);return x.Kind switch{HandleKind.MethodDefinition=>md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)x).Name),HandleKind.MemberReference=>md.GetString(md.GetMemberReference((MemberReferenceHandle)x).Name),_=>""};}).ToArray();
    var direct=Calls("CharacterEnhanceContainer");var capture=Calls("TryCharacterEnhanceGeometry");
    Need(direct.Contains("get__unitEnhance")&&direct.Contains("get_textMeshProUgui")&&direct.Contains("get_parent")&&direct.Contains("MatchesPath"),"typed_direct_control_field_relation_missing");
    Need(capture.Contains("ExactPaths")&&capture.Contains("Current")&&capture.Contains("SelectedOwner")&&capture.Contains("get_onClick"),"current_owner_path_or_control_proof_missing");
    Need(Calls("CurrentCharacterEnhanceGeometry").Contains("TryCharacterEnhanceGeometry")&&Calls("TryPrepareGeometry").Contains("TryCharacterEnhanceGeometry"),"native_late_identity_or_geometry_route_missing");
    Need(Calls("GeometryLayout").Contains("CharacterEnhanceContainer"),"native_owned_button_layout_not_captured");
    Need(!capture.Contains("GlyphFailure")&&!direct.Contains("get_isTextTruncated")&&!direct.Contains("get_preferredWidth"),"visual_failure_would_orphan_ownership_restore");
});
Console.WriteLine(JsonSerializer.Serialize(new{status=failed==0?"PASS":"FAIL",count=tests.Count,failures=failed,
    scope="ACTUAL_CANDIDATE_EXACT_ROLE_AND_COMPILED_CURRENT_BUTTON_PATH_CPU_EXPLICIT_MOCK_IDENTITIES_NOT_NATIVE_PROOF",
    candidate_sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CharacterEnhanceGeometryPolicy).Assembly.Location))).ToLowerInvariant(),native="NOT_RUN",tests},new JsonSerializerOptions{WriteIndented=true}));
return failed==0?0:1;
static IEnumerable<int> Tokens(byte[] bytes){
    var ops=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(x=>x.FieldType==typeof(OpCode)).Select(x=>(OpCode)x.GetValue(null)!).ToDictionary(x=>unchecked((ushort)x.Value));
    for(int i=0;i<bytes.Length;){ushort code=bytes[i++];if(code==0xfe)code=(ushort)(0xfe00|bytes[i++]);var op=ops[code];
        if(op.OperandType==OperandType.InlineMethod)yield return BitConverter.ToInt32(bytes,i);
        i+=op.OperandType switch{OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,
            OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(bytes,i),_=>4};}
}
