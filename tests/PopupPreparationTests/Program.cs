using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Reflection.Emit;
using System.Reflection;
using System.Collections.Immutable;
using System.Text.Json;
using GCMod.Nextgen;
var results=new List<object>(); int fail=0;
void Test(string name,Action action){try{action();results.Add(new{name,status="PASS"});}catch(Exception e){fail++;results.Add(new{name,status="FAIL",reason=e.Message});}}
void Check(bool value){if(!value)throw new Exception("ASSERTION");}
Test("two_distinct_existing_owner_observations_required",()=>{var s=new PopupCaptionReadiness();Check(!s.Observe(true,true,true,false,true,1,"native"));Check(!s.Observe(true,true,true,false,true,1,"native"));Check(s.Observe(true,true,true,false,true,3,"native"));});
Test("native_construction_change_restarts_stability",()=>{var s=new PopupCaptionReadiness();Check(!s.Observe(true,true,true,false,true,1,"before"));Check(!s.Observe(true,true,true,false,true,3,"completed"));Check(s.Observe(true,true,true,false,true,8,"completed"));});
foreach(var flag in new[]{"off_thread","identity_rebound","inactive","out_animation","dirty_source_mesh"})Test(flag+"_clears_prior_readiness",()=>{var s=new PopupCaptionReadiness();s.Observe(true,true,true,false,true,1,"same");Check(!s.Observe(flag!="off_thread",flag!="identity_rebound",flag!="inactive",flag=="out_animation",flag!="dirty_source_mesh",3,"same"));Check(!s.Observe(true,true,true,false,true,8,"same"));});
Test("bounded_window_and_frame_rewind",()=>{var s=new PopupCaptionReadiness();s.Observe(true,true,true,false,true,1,"same");Check(!s.Observe(true,true,true,false,true,122,"same"));Check(!s.Observe(true,true,true,false,true,3,"same"));});
var path=Path.GetFullPath(args[0]);using var doc=JsonDocument.Parse(File.ReadAllText(path));var rows=doc.RootElement.GetProperty("bindings");int supported=0,scoped=0;
foreach(var row in rows.EnumerateArray()) {
 var ss=row.GetProperty("selector"); var si=row.GetProperty("source_identity");
 if(ss.GetProperty("scope_key").GetString() is not ("gc.activeskilldetailpopup" or "gc.exchangelayerpiecepopup"))continue;
 var b=new UiBinding(row.GetProperty("id").GetString()!,row.GetProperty("resource_id").GetString()!,row.GetProperty("adapter_id").GetString()!,row.GetProperty("enabled").GetBoolean(),
  new(si.GetProperty("kind").GetString()!,si.GetProperty("identity").GetRawText(),si.GetProperty("exact_jp").GetString()!),
  new(ss.GetProperty("scope_key").GetString()!,ss.GetProperty("slot_key").GetString()!,ss.GetProperty("segments").EnumerateArray().Select(x=>x.GetString()!).ToImmutableArray(),Type:ss.GetProperty("type").GetString()!,Lifetime:ss.GetProperty("lifetime").GetString()!),null);
 if(PopupCaptionPreparationPolicy.Supports(b))supported++;
 if(PopupCaptionPreparationPolicy.UsesScopedStatus(b))scoped++;
}
Test("accepted_twenty_owner_roles_only",()=>Check(supported==20));Test("only_four_skill_status_geometry_roles",()=>Check(scoped==4));
Test("mission_repeated_cell_roles_preserve_exact_typed_tail",()=>{
 int n=0;
 foreach(var row in rows.EnumerateArray()){
  var sel=row.GetProperty("selector");if(sel.GetProperty("scope_key").GetString()!="gc.mission.missionpopup")continue;
  var si=row.GetProperty("source_identity");var b=new UiBinding(row.GetProperty("id").GetString()!,row.GetProperty("resource_id").GetString()!,row.GetProperty("adapter_id").GetString()!,row.GetProperty("enabled").GetBoolean(),new(si.GetProperty("kind").GetString()!,si.GetProperty("identity").GetRawText(),si.GetProperty("exact_jp").GetString()!),new(sel.GetProperty("scope_key").GetString()!,sel.GetProperty("slot_key").GetString()!,sel.GetProperty("segments").EnumerateArray().Select(x=>x.GetString()!).ToImmutableArray(),Type:sel.GetProperty("type").GetString()!,Lifetime:sel.GetProperty("lifetime").GetString()!),null);
  if(!MissionCellCaptionPolicy.Supports(b))continue;n++;
  Check(MissionCellCaptionPolicy.CellPath(b).Length==5);
  foreach(var changed in new[]{b with{Enabled=false},b with{Selector=b.Selector with{ScopeKey="gc.presentpopup"}},b with{Selector=b.Selector with{SlotKey="ui2-b05"}},b with{Selector=b.Selector with{Segments=b.Selector.Segments.Add("Text")}},b with{SourceIdentity=b.SourceIdentity with{ExactJp="新規"}}})Check(!MissionCellCaptionPolicy.Supports(changed));
 }
 Check(n==4);
});
Test("long_ui6_role_names_keep_actual_sink_reason",()=>{
 var sink=new SafeDiagnostics();
 foreach(var role in new[]{"UI6-UI4-M006-OWNED_CATEGORY_FRAME_TEXT","UI6-UI4-M006-OWNED_CATEGORY_SECTION_TITLE","SHOP_CATEGORY_PHYSICAL_ROLE"}){
   var code=Ui6SourceDiagnosticCodes.Create(role,537,"NO_ENABLED_EXACT_SOURCE_BINDING_L3_H012345ABCDEF");
   Check(code.Length<=96);sink.Record(code);
 }
 Check(!sink.Counters.ContainsKey("INVALID_DIAGNOSTIC_CODE"));Check(sink.Counters.Count==3);
});
Test("six_ui6_roles_have_distinct_report_identity",()=>Check(new[]{"UI6-FILTER-CheckBox","UI6-FILTER-Toggle","UI6-SORT-UI4-M015","UI6-SORT-UI4-M019","UI6-UI4-M006-OWNED_CATEGORY_FRAME_TEXT","UI6-UI4-M006-OWNED_CATEGORY_SECTION_TITLE"}.Select(Ui6SourceDiagnosticCodes.RoleTag).Distinct().Count()==6));
Test("actual_ui6_current_proof_uses_index_and_direct_frame_not_unassigned_cell_cache",()=>{
 using var pe=new PEReader(File.OpenRead(typeof(PopupCaptionReadiness).Assembly.Location));var md=pe.GetMetadataReader();
 var type=md.TypeDefinitions.Single(h=>md.GetString(md.GetTypeDefinition(h).Name)=="GeneralUiProvider");
 var method=md.GetTypeDefinition(type).GetMethods().Single(h=>md.GetString(md.GetMethodDefinition(h).Name)=="Ui6ShopCurrent");
 var refs=Tokens(pe.GetMethodBody(md.GetMethodDefinition(method).RelativeVirtualAddress).GetILBytes()!).Select(t=>{
  var handle=MetadataTokens.EntityHandle(t);if(handle.Kind==HandleKind.MethodSpecification)handle=md.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
  if(handle.Kind==HandleKind.MemberReference){var m=md.GetMemberReference((MemberReferenceHandle)handle);var parent=m.Parent;var owner=parent.Kind==HandleKind.TypeReference?md.GetString(md.GetTypeReference((TypeReferenceHandle)parent).Name):"generic";return owner+"."+md.GetString(m.Name);}
  if(handle.Kind==HandleKind.MethodDefinition)return "local."+md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)handle).Name);return "other";
 }).ToArray();
 Check(!refs.Contains("ShopCategoryScrollCell.get__data"));
 Check(refs.Any(x=>x.EndsWith(".get_dataIndex")));Check(refs.Any(x=>x.EndsWith(".get_Item")));
 Check(refs.Contains("ShopCategoryFrame.get__data"));Check(refs.Any(x=>x.EndsWith(".get_SourceModel")));
});
Console.WriteLine(JsonSerializer.Serialize(new{status=fail==0?"PASS":"FAIL",passed=results.Count-fail,failed=fail,actual_library_sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(PopupCaptionReadiness).Assembly.Location))).ToLowerInvariant(),scope="managed product policy and actual candidate binding inventory; no native proof",results},new JsonSerializerOptions{WriteIndented=true}));return fail==0?0:1;

static IEnumerable<int> Tokens(byte[] bytes){
    var ops=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(x=>x.FieldType==typeof(OpCode)).Select(x=>(OpCode)x.GetValue(null)!).ToDictionary(x=>unchecked((ushort)x.Value));
    for(int i=0;i<bytes.Length;){ushort code=bytes[i++];if(code==0xfe)code=(ushort)(0xfe00|bytes[i++]);var op=ops[code];
        if(op.OperandType==OperandType.InlineMethod)yield return BitConverter.ToInt32(bytes,i);
        i+=op.OperandType switch{OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,
            OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(bytes,i),_=>4};}
}
