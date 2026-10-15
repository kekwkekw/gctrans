using System.Collections.Immutable;
using System.Text.Json;
using GCMod.Nextgen;
using GCMod.Candidate.Native;

var results=new List<object>();int failures=0;
void Test(string name,Action action){try{action();results.Add(new{name,status="PASS"});}catch(Exception ex){failures++;results.Add(new{name,status="FAIL",error=ex.Message});Console.WriteLine(name+": "+ex.Message);}}
void Assert(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
TranslationPackageSnapshot Package(int count,string group="fixture.group",string script="123")
{
    var resources=Enumerable.Range(1,count).Select(i=>new UiResource("r"+i,"choice","literal","source"+i,"문구"+i,"translate",null)).ToArray();
    var rules=resources.Select((r,i)=>new UiBinding("b"+i,r.Id,"story.choice.visual.v1",true,new("choice-group",JsonSerializer.Serialize(new[]{group,script,"branch"+(i+1)}),r.Jp),
        new("story-choice-list","option-label",ImmutableArray<string>.Empty,group,script,"branch"+(i+1)),null));
    return new("fixture","r1","ko","snapshot",new(1,1,1,1),resources,rules,ImmutableArray<PackageFile>.Empty,ImmutableDictionary<string,string>.Empty,MasterTranslationView.Empty,new(true,ImmutableArray<string>.Empty));
}
ChoiceSource[] Sources(int count)=>Enumerable.Range(1,count).Select(i=>new ChoiceSource("123","branch"+i,"source"+i,500,i,100+i,200+i)).ToArray();
Test("T09_translation_and_supported_language_required",()=>{
    foreach(var language in new string?[]{null,"","ja","en","ko-KR","KO","ko","ko-ui3-common-poc"}){
        Assert(!SpecialDisplayPolicy.AllowsWrites(false,language),"Translation OFF allowed native writes");
        Assert(SpecialDisplayPolicy.AllowsWrites(true,language)==(language is "ko" or "ko-ui3-common-poc"),"unsupported language allowed native writes");}
});
Test("T09_config_gate_precedes_native_work",()=>{
    int predicates=0,reads=0;bool NativeCallback(bool access,Func<bool>? enabled){if(!SpecialDisplayPolicy.CanStartNativeWork(access,enabled))return false;reads++;return true;}
    Assert(!NativeCallback(false,()=>{predicates++;return true;})&&!NativeCallback(true,null)&&!NativeCallback(true,()=>false)&&reads==0&&predicates==0,"unavailable config/thread reached native owner");
    Assert(NativeCallback(true,()=>true)&&reads==1,"authorized native work blocked");
});
Test("T22_complete_current_group_and_native_order",()=>{var p=StoryChoicePolicy.Resolve(Package(4),Sources(4).Reverse().ToArray());Assert(p!=null&&p.Options[0].Source.BranchLabel=="branch4","current option order lost");});
Test("T22_supported_added_group_is_not_fixed_four",()=>{var p=StoryChoicePolicy.Resolve(Package(6),Sources(6));Assert(p?.Options.Length==6,"added supported group rejected");});
Test("T22_one_option_mismatch_no_partial_plan",()=>{var s=Sources(4);s[2]=s[2]with{ExactSource="wrong"};Assert(StoryChoicePolicy.Resolve(Package(4),s)==null,"partial group accepted");});
Test("T22_duplicate_branch_fail_closed",()=>{var s=Sources(3);s[2]=s[2]with{BranchLabel="branch1"};Assert(StoryChoicePolicy.Resolve(Package(3),s)==null,"ambiguous branches accepted");});
Test("T22_incomplete_native_list_fail_closed",()=>Assert(StoryChoicePolicy.Resolve(Package(4),Sources(3))==null,"partial current list accepted"));
Test("T22_transaction_preparation_failure_rolls_back_all",()=>{int shown=0,rollback=0;bool ok=ChoiceGroupTransaction.TryCommit(5,i=>i!=4,()=>true,_=>shown++,()=>rollback++);Assert(!ok&&shown==0&&rollback==1,"partial prepared visual committed");});
Test("T23_transaction_commit_failure_rolls_back_all",()=>{var visible=new bool[5];bool ok=ChoiceGroupTransaction.TryCommit(5,_=>true,()=>true,i=>{visible[i]=true;if(i==2)throw new IOException();},()=>Array.Fill(visible,false));Assert(!ok&&visible.All(x=>!x),"partial committed visual remained");});
Test("T23_control_invariance_failure_restores_masks",()=>{int stage=0;var visible=new bool[3];bool ok=ChoiceGroupTransaction.TryCommit(3,_=>true,()=>++stage==1,i=>visible[i]=true,()=>Array.Fill(visible,false));Assert(!ok&&visible.All(x=>!x),"changed input proof retained overlay");});
Test("T09_choice_literal_plan_survives_unrelated_master_disable",()=>{var package=Package(3);var plan=StoryChoicePolicy.Resolve(package,Sources(3))!;Assert(StoryChoicePolicy.IsPlanAvailable(package,plan,MasterTranslationView.Empty),"unrelated Master toggle cancelled literal group");});
Test("T09_choice_retained_master_plan_requires_active_authority",()=>{
    var original=Package(3);var reference=new MasterRef("mNovels","title","source1");var master=new MasterTranslationView(new Dictionary<(string,string,string),string>{{("mNovels","title","source1"),"활성 제목"}},1,"fixture");
    var resources=original.Resources.Values.Select(x=>x.Id=="r1"?x with{Kind="master_ref",Ko=null,MasterRef=reference}:x);
    var package=new TranslationPackageSnapshot(original.Origin,original.Revision,original.Locale,original.Identity,original.Generations,resources,original.Bindings.Values,original.Files,original.ComponentSignatures,master,original.Validation);
    var plan=StoryChoicePolicy.Resolve(package,Sources(3),master)??throw new InvalidOperationException("ready Master group missing");Assert(StoryChoicePolicy.IsPlanAvailable(package,plan,master),"ready Master group rejected");
    Assert(!StoryChoicePolicy.IsPlanAvailable(package,plan,MasterTranslationView.Empty),"retained group used frozen Master while active table disabled");
    var changed=new MasterTranslationView(new Dictionary<(string,string,string),string>{{("mNovels","title","source1"),"다른 제목"}},2,"fixture");
    Assert(!StoryChoicePolicy.IsPlanAvailable(package,plan,changed),"old overlay retained changed Master output");
});
var click=new AnnotationClickProof(4,10,11,12,13,"77",100,true);
var record=new AnnotationRecordProof(click,20,77,"keyword","body","ruby",true);
var popup=new AnnotationPopupProof(30,31,20,77,"keyword","body","ruby");
Test("T24_current_click_record_popup_three_distinct_proofs",()=>Assert(AnnotationProofPolicy.RecordMatches(click,10,12,13,4,20,77,0.2)&&AnnotationProofPolicy.PopupMatches(record,popup),"valid delivered chain rejected"));
Test("T24_wrong_click_token_right_record_rejected",()=>Assert(!AnnotationProofPolicy.RecordMatches(click with{Token="76"},10,12,13,4,20,77,0.2),"wrong token admitted"));
Test("T24_late_asynchronous_record_rejected",()=>Assert(!AnnotationProofPolicy.RecordMatches(click,10,12,13,5,20,77,0.2)&&!AnnotationProofPolicy.RecordMatches(click,10,12,13,4,20,77,3.1),"stale click generation or TTL admitted"));
Test("T24_same_ID_other_record_or_popup_body_rejected",()=>Assert(!AnnotationProofPolicy.PopupMatches(record,popup with{RecordPointer=21})&&!AnnotationProofPolicy.PopupMatches(record,popup with{Description="other"}),"foreign popup admitted"));
Test("T24_delivered_popup_current_physical_instance_required",()=>Assert(AnnotationProofPolicy.PopupIsCurrent(popup,30,31)&&!AnnotationProofPolicy.PopupIsCurrent(popup,30,32)&&!AnnotationProofPolicy.PopupIsCurrent(popup,32,31),"other popup physical instance accepted"));
var label=new UiResource("cost","cost","literal","必要熱情","필요 열정","translate",null);
foreach(var suffix in new[]{"\r\n12\r\n(-5)","  001\t(-02)","\r\n<color=#ff00ff>12</color>\r\n(-5)"})Test("T25_suffix_exact_"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(suffix)))[..8],()=>{Assert(BattleCaptionPolicy.TryCostTitle(label.Jp+suffix,label,MasterTranslationView.Empty,out var result)&&result==label.Ko+suffix,"completed suffix changed");});
Test("T25_no_exact_prefix_no_write",()=>Assert(!BattleCaptionPolicy.TryCostTitle("other必要熱情\r\n1",label,MasterTranslationView.Empty,out _)&&!BattleCaptionPolicy.TryCostTitle("必要熱情x1",label,MasterTranslationView.Empty,out _),"non-label prefix changed"));
var policy=new EpisodeTitleLayoutPolicy("episode-title.fit.v1",FontService.ApprovedProfile,35,ImmutableArray.Create(34,33,32,31),"owned-only","restore-base","episode-list-title");
var measurement=new TitleFitMeasurement("physical","binding","style","제목","제목",35,true,true,true,true,false,true,3,false);
Test("T28_typed_layout_descending_execution_and_restore35",()=>{var current=measurement;var tested=ImmutableArray.Create(35);foreach(int size in policy.DescendingSizes){var d=EpisodeTitleFitPolicy.Decide(policy,measurement,current,tested);Assert(d.Action=="TRY"&&d.Size==size,"grid skipped");current=current with{Size=size};tested=tested.Add(size);}var final=EpisodeTitleFitPolicy.Decide(policy,measurement,current,tested);Assert(final.Action=="RESTORE"&&final.Size==35,"unresolved minimum not restored");});
Test("T28_first_fitting_size_kept",()=>{var m=measurement with{Size=33,Truncated=false,Overflow=-1,FullSequence=true};var d=EpisodeTitleFitPolicy.Decide(policy,measurement,m,ImmutableArray.Create(35,34,33));Assert(d.Action=="KEEP"&&d.Size==33,"largest fitted size lost");});
Test("T28_external_fontsize_yield",()=>Assert(EpisodeTitleFitPolicy.Decide(policy,measurement,measurement with{Size=30},ImmutableArray.Create(35)).Action=="YIELD","external value overwritten"));
Test("T28_style_change_yield",()=>Assert(EpisodeTitleFitPolicy.Decide(policy,measurement,measurement with{ProtectedStyle="other"},ImmutableArray.Create(35)).Action=="YIELD","external style overwritten"));
Test("T28_stale_or_hidden_mesh_wait",()=>Assert(EpisodeTitleFitPolicy.Decide(policy,measurement,measurement with{Fresh=false},ImmutableArray.Create(35)).Action=="WAIT"&&EpisodeTitleFitPolicy.Decide(policy,measurement,measurement with{VisibleInk=false},ImmutableArray.Create(35)).Action=="WAIT","stale geometry resized"));
var identity=new PhysicalTargetIdentity(900,901,"TMP",1);
FloatTargetState State(float value=35,long source=1)=>new(identity,"fontSize",value,2,source,"style",true);
Test("T28_cell_long_short_conditional_owned_restore",()=>{var service=new UiFloatOwnershipService();var target=new FloatFixture(State());Assert(service.TryWrite(target,"Fit3",target.Value,31).Written,"shrink failed");var next=new FloatFixture(State(31,2));Assert(service.RestoreAtRebind(identity,"fontSize",next,next.Value)==RestoreOutcome.Restored&&next.Value.Value==35,"reused short cell remained shrunk");});
Test("T28_cell_external_value_preserved",()=>{var service=new UiFloatOwnershipService();var target=new FloatFixture(State());service.TryWrite(target,"Fit3",target.Value,31);var next=new FloatFixture(State(32,2));Assert(service.RestoreAtRebind(identity,"fontSize",next,next.Value)==RestoreOutcome.ExternalChanged&&next.Value.Value==32,"external size overwritten");});
Test("T28_retryable_restore_retains_float_lease",()=>{var service=new UiFloatOwnershipService();var target=new FloatFixture(State());service.TryWrite(target,"Fit3",target.Value,31);target.FailWrite=true;Assert(service.Restore(identity,"fontSize")==RestoreOutcome.RetryableWriteFailure&&service.LeaseCount==1,"restore evidence discarded");target.FailWrite=false;Assert(service.Restore(identity,"fontSize")==RestoreOutcome.Restored&&target.Value.Value==35,"restore retry failed");});
Test("T09_settled_size_off_restore_and_inactive_retry",()=>{var service=new UiFloatOwnershipService();var target=new FloatFixture(State());service.TryWrite(target,"Fit3",target.Value,31);target.Value=target.Value with{Active=false};Assert(!service.RestoreAll()&&service.LeaseCount==1&&target.Value.Value==31,"inactive settled lease lost during OFF");target.Value=target.Value with{Active=true};Assert(service.RestoreAll()&&service.LeaseCount==0&&target.Value.Value==35,"settled owned size did not restore after activation");});
Test("T09_destroyed_size_yields_without_native_write",()=>{var service=new UiFloatOwnershipService();var target=new FloatFixture(State());service.TryWrite(target,"Fit3",target.Value,31);int writes=target.Writes;target.Value=target.Value with{Active=false,DestroyedOrRebound=true};Assert(service.RestoreAll()&&service.LeaseCount==0&&target.Writes==writes,"destroyed/rebound size received restore write");});
Test("T07_float_offthread_reads_zero",()=>{var service=new UiFloatOwnershipService();var target=new FloatFixture(State()){CanAccess=false};Assert(!service.TryWrite(target,"Fit3",target.Value,34).Written&&target.Reads==0&&target.Writes==0,"offthread target read");});
if(args.Length>1)
{
    Test("T17_current_all_battle_bindings_have_compiled_exact_routes",()=>{
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(args[1],"ui","bindings.json")));var rules=doc.RootElement.GetProperty("bindings").EnumerateArray().Where(x=>x.GetProperty("adapter_id").GetString()=="battle.caption.v1").ToArray();
        foreach(var b in rules){var s=b.GetProperty("selector");var id=b.GetProperty("source_identity").GetProperty("identity").GetRawText();var path=s.TryGetProperty("segments",out var segments)?segments.EnumerateArray().Select(x=>x.GetString()!).ToArray():Array.Empty<string>();var matches=BattleRoutes.All.Where(r=>JsonSerializer.Serialize(r.SourceId)==id&&s.GetProperty("scope_key").GetString()=="battle-"+r.Surface.ToLowerInvariant()&&s.GetProperty("slot_key").GetString()==r.Slot&&path.SequenceEqual(r.Segments)).ToArray();Assert(matches.Length==1,"missing route "+b.GetProperty("id").GetString());}
        Assert(rules.Length==BattleRoutes.All.Length,"route/data baseline drift");
    });
}
string output=Path.GetFullPath(args.Length>0?args[0]:"SPECIAL_TEST_RESULTS.json");Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output,JsonSerializer.Serialize(new{schema="gc-ng02-special-tests-v1",status=failures==0?"PASS":"FAIL",count=results.Count,failures,tests=results,native_execution="NOT_RUN",scope="managed policy and ownership fixtures; no native/game assembly execution"},new JsonSerializerOptions{WriteIndented=true})+"\n");
Console.WriteLine($"SPECIAL {(failures==0?"PASS":"FAIL")} {results.Count-failures}/{results.Count}; {output}");return failures==0?0:1;
sealed class FloatFixture(FloatTargetState initial):IUiFloatTarget,IUiTargetAccessGate
{
    public FloatTargetState Value=initial;public bool CanAccess{get;set;}=true;public bool FailWrite;public int Reads,Writes;
    public FloatTargetState ReadState(){Reads++;return Value;}
    public bool TryWriteFloat(FloatTargetState expected,float value){if(FailWrite)return false;if(Value!=expected)return false;Writes++;Value=Value with{Value=value};return true;}
}
