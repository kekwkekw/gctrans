using System.Text.Json;
using GCMod.Nextgen;
var results=new List<object>();
var click=new AnnotationClickProof(5,10,20,30,40,"7",100,true);
var record=new AnnotationRecordProof(click,50,7,"keyword","body","ruby",false);
var delivered=new AnnotationDeliveredRecord(50,7,"keyword","body","ruby");
void Check(string name,Action check){check();results.Add(new{name,status="PASS"});}
void Require(bool condition){if(!condition)throw new Exception("ANNOTATION_PROOF_REGRESSION");}
bool Eligible(AnnotationClickProof c,long receiver=10,long parent=20,long parentReceiver=10,int instance=30,int thread=40,long generation=5,long pointer=50,int id=7,double age=1,bool claimed=false)=>
 AnnotationDeliveryProofPolicy.RecordEligible(c,receiver,parent,parentReceiver,instance,thread,generation,pointer,id,age,claimed);
Check("actual-factory-record-and-popup-remain-separate",()=>{
 Require(Eligible(click));var selected=AnnotationDeliveryProofPolicy.Select(new[]{record},delivered);Require(selected.Record==record);
 var popup=new AnnotationPopupProof(60,70,50,7,"keyword","body","ruby");Require(AnnotationProofPolicy.PopupMatches(selected.Record!,popup));
 Require(!AnnotationProofPolicy.PopupMatches(selected.Record!,popup with{RecordPointer=51}));Require(!AnnotationProofPolicy.PopupIsCurrent(popup,60,71));
});
Check("unobserved-private-callback-not-factory-precondition",()=>Require(AnnotationDeliveryProofPolicy.Select(new[]{record},delivered).Record!=null));
Check("current-parent-ownership-required",()=>Require(!Eligible(click,parent:21)&&!Eligible(click,parentReceiver:11)));
Check("current-receiver-instance-thread-generation-required",()=>Require(!Eligible(click,receiver:11)&&!Eligible(click,instance:31)&&!Eligible(click,thread:41)&&!Eligible(click,generation:6)));
Check("exact-token-current-story-required",()=>Require(!Eligible(click with{Token="007"})&&!Eligible(click with{CurrentStory=false})));
Check("ttl-and-unclaimed-required",()=>Require(!Eligible(click,age:3.01)&&!Eligible(click,age:-1)&&!Eligible(click,claimed:true)));
Check("zero-actual-record-rejected",()=>Require(!Eligible(click,pointer:0)));
Check("same-id-competing-clicks-held",()=>Require(AnnotationDeliveryProofPolicy.Select(new[]{record,record with{Click=click with{Generation=6}}},delivered).Record==null));
Check("record-field-mutation-held",()=>Require(AnnotationDeliveryProofPolicy.Select(new[]{record},delivered with{Description="changed"}).Record==null));
Check("stale-private-proof-held",()=>Require(AnnotationDeliveryProofPolicy.Select(new[]{record},delivered,record with{Click=click with{Generation=4}}).Record==null));
Check("late-async-factory-alone-remains-unproven",()=>Require(AnnotationDeliveryProofPolicy.Select(new[]{record with{DeliveredAsynchronously=true}},delivered).Reason=="ANNOTATION_ASYNC_FACTORY_CLICK_UNPROVEN"));
Check("existing-separate-private-proof-preserved",()=>{var asynchronous=record with{DeliveredAsynchronously=true};Require(AnnotationDeliveryProofPolicy.Select(new[]{asynchronous},delivered,asynchronous).Record==asynchronous);});
var productAssembly=typeof(AnnotationDeliveryProofPolicy).Assembly;
var assemblySha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(productAssembly.Location))).ToLowerInvariant();
Console.WriteLine(JsonSerializer.Serialize(new{scope="ACTUAL_PRODUCT_ANNOTATION_PROOF_CPU",product_assembly=productAssembly.GetName().Name,product_assembly_sha256=assemblySha,results,native_calls=0,native="NOT_RUN_IN_CPU_SUITE",game="NOT_RUN_IN_CPU_SUITE",source_basis="Product policy called with synthetic identity/field fixture; actual factory callback source and popup remain native trial observations"},new JsonSerializerOptions{WriteIndented=true}));
