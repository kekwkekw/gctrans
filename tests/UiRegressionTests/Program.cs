using GCMod.Nextgen;
using System.Text.Json;
var results = new List<object>(); int failures = 0;
void Test(string name, Action test) { try { test(); results.Add(new { name, status="PASS" }); } catch(Exception e) { failures++;results.Add(new{name,status="FAIL",reason=e.Message}); } }
void Need(bool value,string reason) { if(!value)throw new Exception(reason); }
var donor = new FontDonorHandle(FontService.ApprovedProfile, 7, 9, 1);
GeneralUiFontResult Probe(string text, bool rich=true, Func<int,bool>? medium=null, Func<string,bool>? primary=null)
    => GeneralUiFontPolicy.Check(true,text,rich,donor,()=>donor,medium??GeneralUiFontPolicy.RequiresMedium,primary??(_=>true));
Test("space_absent_in_medium_is_supported_by_real_target_role",()=>{
    var queried=new List<int>();
    var r=Probe("용어 사전",medium:s=>{queried.Add(s);return GeneralUiFontPolicy.RequiresMedium(s);},primary:s=>s=="용어 사전");
    Need(r.Allowed&&!queried.Contains(32)&&queried.Count==4,"incorrect_medium_space_requirement");
});
Test("markup_is_not_a_medium_glyph_but_target_text_remains_complete",()=>{
    string? visible=null;var r=Probe("<color=#8d807a>또는</color> 오른쪽 <cspace=-0.1em>더블 클릭</cspace>",primary:s=>{visible=s;return true;});
    Need(r.Allowed&&visible=="또는 오른쪽 더블 클릭","formatting_projection");
});
Test("required_hangul_missing_is_still_rejected",()=>Need(!Probe("용어 사전",medium:s=>s!='사').Allowed,"missing_hangul_admitted"));
Test("primary_space_or_punctuation_missing_is_still_rejected",()=>Need(!Probe("용어 사전!",primary:s=>!s.Contains(' ')).Allowed,"missing_primary_route_admitted"));
Test("off_thread_performs_zero_native_font_reads",()=>{
    int reads=0;var r=GeneralUiFontPolicy.Check(false,"용어 사전",true,donor,()=>{reads++;return donor;},s=>{reads++;return true;},s=>{reads++;return true;});
    Need(!r.Allowed&&reads==0,"off_thread_native_read");
});
Test("stale_donor_does_not_read_glyph_or_primary",()=>{
    int reads=0;var r=GeneralUiFontPolicy.Check(true,"용어 사전",true,donor,()=>donor with {Generation=2},s=>{reads++;return true;},s=>{reads++;return true;});
    Need(!r.Allowed&&reads==0,"stale_donor_coverage_read");
});
Test("donor_change_during_primary_check_is_held",()=>{
    var current=donor;var r=GeneralUiFontPolicy.Check(true,"용어 사전",true,donor,()=>current,_=>true,_=>{current=donor with {Generation=2};return true;});Need(!r.Allowed,"donor_generation_race");
});
Test("unsupported_or_unbalanced_markup_fails_closed",()=>{
    foreach(var text in new[]{"<sprite=1>가","<noparse>가</noparse>","</color>가","<color=#ffffff>가","<cspace=-.1em>가</cspace>"})Need(!Probe(text).Allowed,"unknown_markup_admitted");
});
Test("literal_angle_characters_are_checked_if_rich_text_is_off",()=>Need(!Probe("<cspace=-0.1em>가</cspace>",false,primary:s=>!s.Contains('<')).Allowed,"literal_tags_hidden"));
Test("strict_global_font_service_was_not_relaxed",()=>{
    var service=new FontService();service.PublishMediumDonor(donor,new HangulOnly());Need(!service.Supports("용어 사전",donor),"global_font_contract_relaxed");
});
Test("footer_composite_does_not_change_other_renderers_leaf_contract",()=>{
    Need(GeneralUiGlyphBoundsPolicy.Contains(true,false,true),"contained_footer_leaf_edge_rejected");
    Need(!GeneralUiGlyphBoundsPolicy.Contains(false,false,true),"non_footer_leaf_overflow_admitted");
    Need(!GeneralUiGlyphBoundsPolicy.Contains(true,true,false),"outside_button_admitted");
});
Test("footer_sdf_padding_is_separate_from_metric_ink_and_painted_effects",()=>{
    Need(UiGlyphInkBounds.TryGet(0,0,1,0,17,20,33,0,-3,-20,23,21,out var ink),"valid_metric_ink_rejected");
    Need(ink==(0f,-16f,20f,17f),"metric_projection");
    Need(UiGlyphInkBounds.TryGet(0,0,1,0,17,20,33,2,-3,-20,23,21,out var painted)&&painted.Bottom==-18,"outline_not_included");
    Need(!UiGlyphInkBounds.TryGet(0,0,1,0,17,20,33,8,-3,-20,23,21,out _),"unproven_paint_outside_quad_admitted");
});
Test("invalid_glyph_metrics_cannot_shrink_a_real_overflow",()=>{
    Need(!UiGlyphInkBounds.TryGet(0,0,0,0,17,20,33,0,-3,-20,23,21,out _),"zero_scale");
    Need(!UiGlyphInkBounds.TryGet(float.NaN,0,1,0,17,20,33,0,-3,-20,23,21,out _),"nonfinite_origin");
    Need(!UiGlyphInkBounds.TryGet(0,0,1,0,17,20,33,-1,-3,-20,23,21,out _),"negative_effect");
    Need(!UiGlyphInkBounds.TryGet(0,0,1,0,17,80,33,0,-3,-20,23,21,out _),"ink_outside_current_quad");
});
Test("mask_channel_truncation_source_and_control_failures_still_restore",()=>{
    foreach(var f in new[]{UiVisualFailure.IntrinsicOverflow,UiVisualFailure.MaskClipped,UiVisualFailure.RenderChannelHidden,UiVisualFailure.SourceChanged,UiVisualFailure.OwnerRebound,UiVisualFailure.ControlChanged})
      Need(UiVisualSettlement.Assess(3,f,true).Action==UiVisualAction.Restore,"unsafe_settlement_admitted");
});
Test("actual_module_host_uses_role_coverage_and_preserves_pre_read_gate",()=>{
    string root=Path.Combine(Path.GetTempPath(),"gc-ui-regression-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"ui"));
    File.WriteAllText(Path.Combine(root,"ui/resources.json"),"""
    {"schema_version":1,"locale":"ko","resources":[{"id":"menu.download","role":"menu.caption","kind":"literal","jp":"辞典","ko":"용어 사전","disposition":"translate"}]}
    """);
    File.WriteAllText(Path.Combine(root,"ui/bindings.json"),"""
    {"schema_version":1,"bindings":[{"id":"binding.download","resource_id":"menu.download","adapter_id":"core.static-caption.v1","enabled":true,"source_identity":{"kind":"static-asset","identity":"fixture:font-role","exact_jp":"辞典"},"selector":{"scope_key":"main-menu","slot_key":"download-label","type":"static","segments":["Text"]}}]}
    """);
    File.WriteAllText(Path.Combine(root,"master.json"),"{}");File.WriteAllText(Path.Combine(root,"names.json"),"{}");
    var files=new List<object>();
    foreach(var (p,k) in new[]{("ui/resources.json","resources"),("ui/bindings.json","bindings"),("master.json","master"),("names.json","names")})
    {var bytes=File.ReadAllBytes(Path.Combine(root,p));files.Add(new{path=p,kind=k,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),size=bytes.Length});}
    File.WriteAllText(Path.Combine(root,"ui/manifest.json"),JsonSerializer.Serialize(new{schema_version=1,locale="ko",origin="local:cpu-fixture",revision="font-role",files}));
    var registry=AdapterRegistry.CreateNg01();registry.RegisterHostAdapter("core.static-caption.v1",SelectorContract.Host(["main-menu"],["download-label"]));
    var snapshot=new PackageLoader(new LocalPackageFileSource(),registry).LoadAsync(root).GetAwaiter().GetResult();var b=snapshot.Bindings["binding.download"];
    var font=new FontService();font.PublishMediumDonor(donor,new HangulOnly());var owned=new UiOwnershipService();var host=new UiModuleHost(snapshot,registry,owned,fonts:font);
    var initial=new TextTargetState(new(11,12,"MOCK_TMP",1),"辞典",1,1,b.SourceIdentity.IdentityJson,"辞典");var target=new RoleTarget(initial,donor);
    var facts=new BindingFacts(b.SourceIdentity.IdentityJson,"辞典","menu.caption","main-menu","download-label",["Text"],1,1);
    var evidence=new UiEvidence(MainThread:true,CurrentOwner:true,UniqueTarget:true,DirectTarget:true,NativeControlsUnchanged:true,InputUntouched:true);
    var context=new UiBindContext(b.Id,target,facts,evidence,snapshot.Generations.Ui,HostFixture:true,FontDonor:donor);
    var result=host.TryApply(context);Need(result.Written&&target.State.Text=="용어 사전"&&target.CoverageReads==1,"actual_host_skipped_role_coverage");
    int reads=target.Reads,coverage=target.CoverageReads;Need(!host.TryApply(context with{Evidence=evidence with{MainThread=false}}).Written&&target.Reads==reads&&target.CoverageReads==coverage,"host_read_before_main_thread_gate");
});
Console.WriteLine(JsonSerializer.Serialize(new{status=failures==0?"PASS":"FAIL",count=results.Count,failures,scope="ACTUAL_CANDIDATE_CPU_WITH_EXPLICIT_MOCK_FONT_INPUTS_NOT_NATIVE_PROOF",candidate_sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(GeneralUiFontPolicy).Assembly.Location))).ToLowerInvariant(),results},new JsonSerializerOptions{WriteIndented=true}));
return failures==0?0:1;
sealed class HangulOnly:IFontGlyphProvider { public bool HasGlyph(FontDonorHandle donor,int scalar)=>GeneralUiFontPolicy.RequiresMedium(scalar); }
sealed class RoleTarget(TextTargetState initial,FontDonorHandle donor):IUiTextTarget,IGeneralUiFontTarget
{
    public TextTargetState State=initial;public int Reads,CoverageReads;
    public TextTargetState ReadState(){Reads++;return State;}
    public bool TryWriteText(TextTargetState expected,string text){if(expected!=State)return false;State=State with{Text=text};return true;}
    public GeneralUiFontResult CheckGeneralUiFont(string text,FontDonorHandle expected,FontService service)
    {CoverageReads++;return GeneralUiFontPolicy.Check(true,text,true,expected,()=>service.Current,s=>service.Supports(new System.Text.Rune(s).ToString(),donor),s=>s.All(c=>"용어 사전".Contains(c)));}
}
