#nullable enable
using GCMod.Nextgen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GCMod.Candidate.Native;
public sealed partial class GeneralUiProvider
{
    sealed class FilterCaptionPending(TextMeshProUGUI text, UiBinding binding, CoreProducerIdentity producer, FilterCaptionActivationIdentity identity)
    {
        public readonly TextMeshProUGUI Text=text;
        public readonly UiBinding Binding=binding;
        public readonly CoreProducerIdentity Producer=producer;
        public readonly FilterCaptionActivationIdentity Identity=identity;
        public readonly FilterCaptionActivationState State=new(identity);
        public int Rearms=0;
    }
    readonly Dictionary<(long,string),FilterCaptionPending> filterCaptionPending=new();
    bool TryFilterEntryIdentity(Owner owner, TextMeshProUGUI text, UiBinding binding, out FilterCaptionActivationIdentity? identity)
    {
        gate.Require();identity=null;
        var role=FilterResetGeometryPolicy.Role(binding);
        if(role is not (FilterPreferredCaptionRole.SelectAll or FilterPreferredCaptionRole.Deselect)
            || !Current(owner) || owner.Value is not Gc.FilterPopup popup || !Exact(popup,"Gc.FilterPopup") || !Alive(text))return false;
        var button=role==FilterPreferredCaptionRole.SelectAll?popup._selectAllButton:popup._deselectAllButton;
        int matches=0;
        foreach(var candidate in new[]{popup._defaultAllButton,popup._deselectAllButton,popup._selectAllButton})
            if(Alive(candidate)&&Ptr(candidate)==Ptr(button))matches++;
        if(!Alive(button)||!Exact(button,"UnityEngine.UI.InertButton")
            || matches!=1
            || Ptr(button!.textMeshProUgui)!=Ptr(text)||Ptr(Parent<Button>(text))!=Ptr(button)
            || !text.transform.IsChildOf(popup.transform)||text.GetComponents<TextMeshProUGUI>().Length!=1)return false;
        var leaves=ExactPaths(popup.transform,binding.Selector.Segments);
        if(leaves.Count!=1||Ptr(leaves[0])!=Ptr(text.transform))return false;
        // Inactive controls may be observed here; geometry/write still requires its original active proof.
        identity=new(Ptr(popup),owner.Instance,owner.Lifetime,owner.Scene,Ptr(button),button.GetInstanceID(),Ptr(text),text.GetInstanceID(),host.Snapshot.Generations.Ui,role);
        return true;
    }
    void TrackFilterCaption(Owner owner, TextMeshProUGUI text, UiBinding binding, CoreProducerIdentity producer)
    {
        gate.Require();
        if(!TryFilterEntryIdentity(owner,text,binding,out var identity))return;
        var key=(owner.Lifetime,binding.Id);
        if(filterCaptionPending.TryGetValue(key,out var old))
        {if(old.Identity!=identity||old.Producer!=producer)filterCaptionPending.Remove(key);return;}
        if(filterCaptionPending.Count>=128)return;
        filterCaptionPending[key]=new(text,binding,producer,identity!);
        FilterEntryReport(owner,filterCaptionPending[key],"PENDING_VALIDATED");
    }
    void FilterEntryReport(Owner owner,FilterCaptionPending pending,string phase)
    {
        string id=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pending.Binding.Id)))[..12];
        log.Record($"FENTRY_{id}_O{owner.Lifetime}_U{host.Snapshot.Generations.Ui}_{phase}_R{(int)pending.Identity.Role}_E{pending.Rearms}");
    }
    bool TickFilterCaptionReadiness(ref Owner owner)
    {
        gate.Require();
        if(owner.Value is not Gc.FilterPopup popup)return false;
        bool retained=false;
        foreach(var pair in filterCaptionPending.ToArray())
        {
            if(pair.Key.Item1!=owner.Lifetime)continue;
            var p=pair.Value;var button=p.Identity.Role==FilterPreferredCaptionRole.SelectAll?popup._selectAllButton:popup._deselectAllButton;
            bool direct=Current(owner)&&Alive(button)&&button!.GetInstanceID()==p.Identity.ButtonInstance&&Ptr(button)==p.Identity.Button
                &&Alive(p.Text)&&p.Text.GetInstanceID()==p.Identity.TargetInstance&&Ptr(button.textMeshProUgui)==p.Identity.Target;
            bool source=direct&&CoreSourceCurrent(owner,p.Text,p.Binding);var donor=fonts.Current;
            bool active=direct&&owner.Value.gameObject.activeInHierarchy&&button!.gameObject.activeInHierarchy&&p.Text.enabled&&p.Text.gameObject.activeInHierarchy
                &&(p.Identity.Role!=FilterPreferredCaptionRole.SelectAll||!popup._forceHideSelectAllButton);
            var current=p.Identity with{UiGeneration=host.Snapshot.Generations.Ui};
            var result=p.State.Observe(new(current,true,Current(owner),direct,source,active,donor!=null,donor?.Generation??0));
            if(result==FilterCaptionActivationResult.Invalidated)
            {if(p.Rearms<8)FilterEntryReport(owner,p,"INVALIDATED");filterCaptionPending.Remove(pair.Key);continue;}
            if(result==FilterCaptionActivationResult.Rearm)
            {
                if(!TryFilterEntryIdentity(owner,p.Text,p.Binding,out var rechecked)||rechecked!=p.Identity||!CoreProducerCurrent(owner,p.Text,p.Binding,p.Producer))
                {if(p.Rearms<8)FilterEntryReport(owner,p,"READY_PROOF_REJECTED");filterCaptionPending.Remove(pair.Key);continue;}
                owner=owner with{Started=frame-1};owners[Ptr(owner.Value)]=owner;p.Rearms++;
                if(p.Rearms<=8)FilterEntryReport(owner,p,"READY_EDGE_REARM");
            }
            retained=true;
        }
        return retained;
    }
    void ForgetFilterCaptionEntry(long lifetime)
    {
        foreach(var key in filterCaptionPending.Keys.Where(k=>k.Item1==lifetime).ToArray())filterCaptionPending.Remove(key);
    }
}
