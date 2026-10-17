using GCMod.Candidate.Native;
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
var live=new ShopHeaderGraphIdentity(11,12,12,13,13,14,15,15,16,16,1,1);
Test("current_view_model_and_global_direct_header_graph",()=>Need(ShopHeaderCurrentTabPolicy.Matches(live),"valid_explicit_mock_graph_rejected"));
Test("all_missing_or_mismatched_graph_roles_rejected",()=>{
    foreach(var value in new[]{live with{Page=0},live with{View=0},live with{PageView=120},live with{Model=0},live with{ViewModel=130},
        live with{Controller=0},live with{Header=0},live with{CurrentHeader=150},live with{Target=0},live with{HeaderTarget=160},live with{Target=15,HeaderTarget=15},
        live with{CurrentTab=-1,CallbackTab=-1},live with{CallbackTab=2}})
        Need(!ShopHeaderCurrentTabPolicy.Matches(value),"invalid_identity_graph_admitted");
});
Test("current_unique_subscene_page_only",()=>{
    Need(ShopHeaderCurrentTabPolicy.SelectedPage(11,1,new long[]{4,11,5}),"unique_selected_page_rejected");
    foreach(var (page,index,pages) in new[]{(0L,0,new long[]{0}),(11L,-1,new long[]{11}),(11L,1,new long[]{11}),(11L,0,new long[]{4,11}),
        (11L,0,new long[]{11,11}),(11L,0,Enumerable.Repeat(11L,33).ToArray()),(11L,0,Array.Empty<long>())})
        Need(!ShopHeaderCurrentTabPolicy.SelectedPage(page,index,pages),"noncurrent_or_duplicate_scene_admitted");
});
var heading=new UiBinding("fixture.shop.heading","fixture.resource","ui.caption.v1",true,new("current-model","[]","ショップ"),
    new("gc.shop.shoptopcontroller","shop_title",ImmutableArray<string>.Empty,Type:"caption",Lifetime:"owner"),null);
Test("owned_shop_container_policy_is_exact_typed_role_only",()=>{
    Need(ShopHeaderGeometryPolicy.Supports(heading),"current_shop_heading_role_rejected");
    foreach(var b in new[]{heading with{Enabled=false},heading with{AdapterId="ui.residual-caption.v1"},
        heading with{SourceIdentity=heading.SourceIdentity with{Kind="static-asset"}},heading with{SourceIdentity=heading.SourceIdentity with{ExactJp="異なる見出し"}},
        heading with{Selector=heading.Selector with{ScopeKey="gc.headertopcontroller"}},heading with{Selector=heading.Selector with{SlotKey="body"}},
        heading with{Selector=heading.Selector with{Type="static"}},heading with{Selector=heading.Selector with{Lifetime="cell"}},
        heading with{Selector=heading.Selector with{Segments=ImmutableArray.Create("unknown")}}})
        Need(!ShopHeaderGeometryPolicy.Supports(b),"unrelated_scope_authorized_shop_container");
});
Test("full_quad_material_preserves_known_paint_and_blocks_unknown_shader_or_keyword",()=>{
    foreach(var shader in new[]{"TextMeshPro/Mobile/Distance Field","TextMeshPro/Distance Field"})
        Need(ShopHeaderGeometryPolicy.SupportedQuadMaterial(shader,new[]{"UNDERLAY_ON","OUTLINE_ON","UNITY_UI_CLIP_RECT"}),"supported_full_quad_material_rejected");
    Need(!ShopHeaderGeometryPolicy.SupportedQuadMaterial("Unknown/Distance Field",Array.Empty<string>()),"unknown_shader_allowed");
    Need(!ShopHeaderGeometryPolicy.SupportedQuadMaterial("Unknown/Distance Field",new[]{"OUTLINE_ON"}),"outline_keyword_authorized_unknown_shader");
    Need(!ShopHeaderGeometryPolicy.SupportedQuadMaterial("TextMeshPro/Distance Field",new[]{"UNKNOWN_VERTEX_EXTRUSION"}),"unknown_shader_keyword_allowed");
    Need(!ShopHeaderGeometryPolicy.SupportedQuadMaterial("TextMeshPro/Distance Field",Enumerable.Repeat("UNITY_UI_CLIP_RECT",33)),"unbounded_keywords_allowed");
});
Test("actual_candidate_retry_reaches_real_capture_and_write_reuses_existing_driver",()=>{
    using var pe=new PEReader(File.OpenRead(typeof(ShopHeaderCurrentTabPolicy).Assembly.Location));var md=pe.GetMetadataReader();
    var handle=md.TypeDefinitions.Single(h=>md.GetString(md.GetTypeDefinition(h).Name)=="GeneralUiProvider");
    var methods=md.GetTypeDefinition(handle).GetMethods().Where(h=>md.GetString(md.GetMethodDefinition(h).Name) is "RetryShopHeader" or "CaptureShopHeader" or "ApplyShopHeader").ToDictionary(h=>md.GetString(md.GetMethodDefinition(h).Name));
    string[] Calls(string name)=>Tokens(pe.GetMethodBody(md.GetMethodDefinition(methods[name]).RelativeVirtualAddress).GetILBytes()!).Select(token=>{
        var h=MetadataTokens.EntityHandle(token);return h.Kind switch{HandleKind.MethodDefinition=>md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)h).Name),
            HandleKind.MemberReference=>md.GetString(md.GetMemberReference((MemberReferenceHandle)h).Name),_=>""};}).ToArray();
    Need(Calls("RetryShopHeader").Contains("CaptureShopHeader")&&Calls("RetryShopHeader").Contains("Matches"),"natural_callback_fallback_not_wired");
    Need(Calls("CaptureShopHeader").Contains("ApplyShopHeader")&&Calls("ApplyShopHeader").Contains("ApplyDirect"),"existing_owned_writer_bypassed");
});
Test("actual_candidate_shop_renderer_remains_current_owned_full_quad_path",()=>{
    using var pe=new PEReader(File.OpenRead(typeof(ShopHeaderCurrentTabPolicy).Assembly.Location));var md=pe.GetMetadataReader();
    var handle=md.TypeDefinitions.Single(h=>md.GetString(md.GetTypeDefinition(h).Name)=="GeneralUiProvider");
    var methods=md.GetTypeDefinition(handle).GetMethods().Where(h=>md.GetString(md.GetMethodDefinition(h).Name) is "TryPrepareGeometry" or "TryShopHeaderGeometry" or "ShopHeaderPlainMaterial" or "CurrentShopHeaderGeometry" or "ShopHeaderContainer").ToDictionary(h=>md.GetString(md.GetMethodDefinition(h).Name));
    string[] Calls(string name)=>Tokens(pe.GetMethodBody(md.GetMethodDefinition(methods[name]).RelativeVirtualAddress).GetILBytes()!).Select(token=>{
        var h=MetadataTokens.EntityHandle(token);return h.Kind switch{HandleKind.MethodDefinition=>md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)h).Name),
            HandleKind.MemberReference=>md.GetString(md.GetMemberReference((MemberReferenceHandle)h).Name),_=>""};}).ToArray();
    Need(Calls("TryPrepareGeometry").Contains("TryShopHeaderGeometry")&&Calls("TryPrepareGeometry").Contains("GlyphFailure"),"shop_full_quad_renderer_not_wired");
    Need(Calls("TryShopHeaderGeometry").Contains("ShopHeaderSourceCurrent")&&Calls("TryShopHeaderGeometry").Contains("ShopHeaderContainer")&&Calls("CurrentShopHeaderGeometry").Contains("TryShopHeaderGeometry"),"current_source_identity_guard_missing");
    Need(!Calls("TryShopHeaderGeometry").Contains("ShopHeaderContainerReady")&&!Calls("CurrentShopHeaderGeometry").Contains("ShopHeaderContainerReady"),"visual_failure_misclassified_as_owner_rebound_during_restore");
    Need(Calls("ShopHeaderContainer").Contains("get__subSceneTitleText")&&Calls("ShopHeaderContainer").Contains("get__frameUI")&&Calls("ShopHeaderContainer").Contains("get_parent"),"typed_direct_container_field_relation_missing");
    Need(Calls("ShopHeaderPlainMaterial").Contains("SupportedQuadMaterial")&&!Calls("ShopHeaderPlainMaterial").Contains("FooterVisibleCorners"),"shop_material_bypassed_or_footer_ink_imported");
});
Console.WriteLine(JsonSerializer.Serialize(new{status=failed==0?"PASS":"FAIL",count=tests.Count,failures=failed,
    scope="ACTUAL_CANDIDATE_PRIMITIVE_IDENTITY_AND_COMPILED_PATH_CPU_EXPLICIT_MOCK_VALUES_NOT_NATIVE_PROOF",
    candidate_sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(ShopHeaderCurrentTabPolicy).Assembly.Location))).ToLowerInvariant(),
    native="NOT_RUN",tests},new JsonSerializerOptions{WriteIndented=true}));return failed==0?0:1;
static IEnumerable<int> Tokens(byte[] bytes){
    var ops=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(x=>x.FieldType==typeof(OpCode)).Select(x=>(OpCode)x.GetValue(null)!).ToDictionary(x=>unchecked((ushort)x.Value));
    for(int i=0;i<bytes.Length;){ushort code=bytes[i++];if(code==0xfe)code=(ushort)(0xfe00|bytes[i++]);var op=ops[code];
        if(op.OperandType==OperandType.InlineMethod)yield return BitConverter.ToInt32(bytes,i);
        i+=op.OperandType switch{OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,
            OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(bytes,i),_=>4};}
}
