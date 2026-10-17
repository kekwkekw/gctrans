using GCMod.Nextgen;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection.Emit;
using System.Reflection.Metadata.Ecma335;

var tests=new List<object>();int failed=0;
void Need(bool value,string why){if(!value)throw new InvalidDataException(why);}
void Test(string name,Action work){try{work();tests.Add(new{name,status="PASS"});}catch(Exception e){failed++;tests.Add(new{name,status="FAIL",reason=e.Message});}}
var select=new UiBinding("fixture.select","fixture.resource","ui4.static-caption.v1",true,new("static-asset","[]","全て選択"),
    new("gc.filterpopup","ui4-ui4-s085-82-06007a91",ImmutableArray.Create("Frame_Popup","PopupWindowM","PopupBase","ContentsArea","Frame_Content","Group_Bt","BtSelectAll","image_Textbtn01_s","Group_Text","Text_Textbtn01"),Type:"static",Lifetime:"owner"),null);
var help=new UiBinding("fixture.help","fixture.resource","ui.residual-caption.v1",true,new("current-model","[]","オーブ・下位命鏡・無名の命鏡を藝術家の命鏡に変換できます"),
    new("gc.shop.shoptopcontroller","exchange_conversion_help",ImmutableArray.Create("Animation_Main","Tab_Parent","Content_Tab_Parent","Tab_Child","Content_Tab_Child_Exchange","Tab_Child_Header","Text_Message"),Type:"exchange",Lifetime:"owner"),null);
Test("selectall_exact_role_is_distinct",()=>{
    Need(FilterResetGeometryPolicy.Role(select)==FilterPreferredCaptionRole.SelectAll,"exact_role_missing");
    foreach(var b in new[]{select with{Enabled=false},select with{SourceIdentity=select.SourceIdentity with{ExactJp="全て解除"}},select with{Selector=select.Selector with{SlotKey="ui4-ui4-s064-62-06007a91"}},select with{Selector=select.Selector with{Segments=select.Selector.Segments.SetItem(6,"BtDefaultAll")}},select with{Selector=select.Selector with{ScopeKey="gc.mission.missionpopup"}},select with{AdapterId="ui.residual-caption.v1"}})
        Need(!FilterResetGeometryPolicy.Supports(b),"wrong_source_button_slot_owner_accepted");
});
Test("preferred_axis_not_pixel_allowance",()=>{
    Need(NativeContentFitPolicy.Matches(true,false,130,28,121,28,121,20),"native_preferred_change_rejected");
    Need(!NativeContentFitPolicy.Matches(false,false,130,28,121,28,121,20),"missing_driver_accepted");
    Need(!NativeContentFitPolicy.Matches(true,false,130,28,121,29,121,20),"undriven_axis_accepted");
    Need(!NativeContentFitPolicy.Matches(true,false,130,28,121,28,130,20),"arbitrary_width_accepted");
});
Test("shop_help_exact_owner_role_path",()=>{
    Need(ShopHelpGeometryPolicy.Supports(help),"exact_help_rejected");
    foreach(var b in new[]{help with{Enabled=false},help with{AdapterId="ui.caption.v1"},help with{Selector=help.Selector with{ScopeKey="gc.exchangelayerpiecepopup"}},help with{Selector=help.Selector with{SlotKey="shop_title"}},help with{Selector=help.Selector with{Segments=help.Selector.Segments.SetItem(5,"Frame_Content")}},help with{SourceIdentity=help.SourceIdentity with{Kind="static-asset"}}})
        Need(!ShopHelpGeometryPolicy.Supports(b),"other_popup_header_or_container_accepted");
});
Test("full_quad_outside_leaf_inside_declared_region",()=>{
    float[] quad={-410,-17,-410,16,414,16,414,-17};
    Need(!ShopHelpGeometryPolicy.FullQuadInside(-320,-16,320,16,quad),"leaf_incorrectly_fits");
    Need(ShopHelpGeometryPolicy.FullQuadInside(-500,-35,500,35,quad),"declared_region_false_overflow");
    Need(!ShopHelpGeometryPolicy.FullQuadInside(-400,-35,500,35,quad),"actual_left_clip_accepted");
    Need(!ShopHelpGeometryPolicy.FullQuadInside(-500,-10,500,35,quad),"actual_bottom_clip_accepted");
    Need(!ShopHelpGeometryPolicy.FullQuadInside(-500,-35,500,35,new float[]{float.NaN,0,0,0,0,0,0,0}),"invalid_transform_accepted");
    Need(!ShopHelpGeometryPolicy.FullQuadInside(-500,-35,500,35,quad[..6]),"missing_corner_accepted");
});
Test("actual_candidate_inventory_selects_both_runtime_policies",()=>{
    if(args.Length!=1)throw new ArgumentException("actual bindings.json is required");
    using var d=JsonDocument.Parse(File.ReadAllBytes(args[0]));
    foreach(var (id,support) in new (string,Func<UiBinding,bool>)[]{("binding.core.UI4-S085.a38b49f27321ec8a",FilterResetGeometryPolicy.Supports),("binding.residuals.UI12-U1-001.6242e5f107c76fd3",ShopHelpGeometryPolicy.Supports)})
    {
        var r=d.RootElement.GetProperty("bindings").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id);var s=r.GetProperty("selector");var x=r.GetProperty("source_identity");
        var b=new UiBinding(id,r.GetProperty("resource_id").GetString()!,r.GetProperty("adapter_id").GetString()!,r.GetProperty("enabled").GetBoolean(),new(x.GetProperty("kind").GetString()!,x.GetProperty("identity").GetRawText(),x.GetProperty("exact_jp").GetString()!),new(s.GetProperty("scope_key").GetString()!,s.GetProperty("slot_key").GetString()!,s.GetProperty("segments").EnumerateArray().Select(x=>x.GetString()!).ToImmutableArray(),Type:s.GetProperty("type").GetString(),Lifetime:s.GetProperty("lifetime").GetString()),null);
        Need(support(b),"actual_package_role_not_supported:"+id);
    }
});
Test("full_region_does_not_skip_source_clip_control_glyph_checks",()=>{
    Need(ShopHelpGeometryPolicy.RegionEligible(true,true,true,true,true,true,true,true),"explicit_managed_positive");
    for(int i=0;i<8;i++){var f=Enumerable.Repeat(true,8).ToArray();f[i]=false;Need(!ShopHelpGeometryPolicy.RegionEligible(f[0],f[1],f[2],f[3],f[4],f[5],f[6],f[7]),"failed_proof_admitted_"+i);}
});
Test("unknown_shader_clip_or_material_is_held",()=>{
    Need(ShopHelpGeometryPolicy.SupportedFullQuadMaterial("TextMeshPro/Mobile/Distance Field",new[]{"UNDERLAY_ON"}),"stock_full_quad_shader_rejected");
    foreach(var x in new[]{("custom/TMP",Array.Empty<string>()),("TextMeshPro/Mobile/Distance Field",new[]{"CUSTOM_CLIP"}),("TextMeshPro/Distance Field",new[]{"UNITY_UI_CLIP_RECT"}),("TextMeshPro/Distance Field",new[]{"UNITY_UI_ALPHACLIP"})})
        Need(!ShopHelpGeometryPolicy.SupportedFullQuadMaterial(x.Item1,x.Item2),"unproven_shader_clip_admitted");
});
var path=typeof(ShopHelpGeometryPolicy).Assembly.Location;using var pe=new PEReader(File.OpenRead(path));var md=pe.GetMetadataReader();
IEnumerable<string> Calls(string type,string method){var td=md.TypeDefinitions.Select(h=>md.GetTypeDefinition(h)).Single(t=>md.GetString(t.Name)==type);var m=td.GetMethods().Select(h=>md.GetMethodDefinition(h)).Single(m=>md.GetString(m.Name)==method);var bytes=pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes()!;var ops=typeof(OpCodes).GetFields().Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(o=>(ushort)o.Value);
 for(int i=0;i<bytes.Length;){ushort k=bytes[i++];if(k==0xfe)k=(ushort)(0xfe00|bytes[i++]);var op=ops[k];if(op.OperandType==OperandType.InlineMethod){var h=MetadataTokens.EntityHandle(BitConverter.ToInt32(bytes,i));if(h.Kind==HandleKind.MemberReference)yield return md.GetString(md.GetMemberReference((MemberReferenceHandle)h).Name);else if(h.Kind==HandleKind.MethodDefinition)yield return md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)h).Name);}i+=op.OperandType switch{OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(bytes,i),_=>4};}}
Test("actual_native_adapter_reads_selectall_field_and_revalidates_typed_driver",()=>{
    var call=Calls("GeneralUiProvider","TryFilterResetIdentity").ToArray();Need(call.Contains("get__selectAllButton")&&call.Contains("ExactPaths"),"typed_selectall_identity_not_wired");
    Need(Calls("GeneralUiProvider","TryFilterResetGeometry").Contains("CaptureContentFitDriver"),"native_driver_missing");
    Need(Calls("GeneralUiProvider","CurrentFilterResetIdentity").Contains("TryFilterResetIdentity"),"stale_owner_not_revalidated");
});
Test("actual_entrypoint_uses_shop_row_proof_without_changing_other_renderers",()=>{
    var prep=Calls("GeneralUiProvider","TryPrepareGeometry").ToArray();Need(prep.Contains("TryShopHelpGeometry")&&prep.Contains("ShopHelpRegionProof"),"actual_prepare_not_wired");
    Need(Calls("GeneralUiProvider","CurrentShopHelpGeometry").Contains("TryShopHelpGeometry"),"current_tab_owner_revalidation_missing");
    Need(Calls("GeneralUiProvider","TickGeometry").Contains("ShopHelpRegionProof"),"post_write_region_not_checked");
    Need(Calls("GeneralUiProvider","ShopHelpRegionProof").Contains("RegionEligible"),"managed_policy_not_called");
});
Test("cached_shop_tab_rearms_existing_bounded_current_row_batch",()=>{
    var calls=Calls("GeneralUiProvider","RearmCurrentExchangeRows").ToArray();
    Need(calls.Contains("Require")&&calls.Contains("SelectedPage"),"native_read_gate_or_current_page_missing");
    Need(calls.Contains("get__viewData")&&calls.Contains("get__ShopDataList_k__BackingField")&&calls.Contains("get__CurrentTabIndex_k__BackingField"),"actual_current_tab_model_missing");
    Need(calls.Contains("EnsureScrollReceipt")&&calls.Contains("ArmScrollBatch"),"existing_bounded_batch_not_used");
    Need(!calls.Any(c=>c.StartsWith("set_")||c=="Setup"||c=="ForceMeshUpdate"),"native_state_or_producer_replayed");
    var td=md.TypeDefinitions.Select(h=>md.GetTypeDefinition(h)).Single(t=>md.GetString(t.Name)=="GeneralUiProvider");
    var callbacks=td.GetMethods().Select(h=>md.GetMethodDefinition(h)).Select(m=>md.GetString(m.Name)).Where(n=>n.StartsWith("<Initialize>"));
    Need(callbacks.Any(n=>Calls("GeneralUiProvider",n).Contains("RearmCurrentExchangeRows")),"verified_existing_tab_callback_not_wired");
});
Console.WriteLine(JsonSerializer.Serialize(new{status=failed==0?"PASS":"FAIL",count=tests.Count,failed,actual_library_sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),scope="ACTUAL_PRODUCT_POLICY_MATH_AND_PE_CALL_WIRING; explicit managed mock inputs; native proof NOT_RUN",tests},new JsonSerializerOptions{WriteIndented=true}));return failed==0?0:1;
