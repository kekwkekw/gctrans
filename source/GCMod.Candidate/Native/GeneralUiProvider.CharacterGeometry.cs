#nullable enable
using GCMod.Nextgen;
using TMPro;
using UnityEngine;

namespace GCMod.Candidate.Native;
public sealed partial class GeneralUiProvider
{
    sealed record CharacterEnhanceGeometryIdentity(long Owner,int OwnerInstance,long Button,int ButtonInstance,
        long Text,int TextInstance,long Container,int ContainerInstance,long Controller,int ControllerInstance,long Click,long Lifetime);
    readonly HashSet<string> characterEnhanceGeometryReports=new(StringComparer.Ordinal);

    static RectTransform? CharacterEnhanceContainer(TextMeshProUGUI text)
    {
        // Identity only. A changed visual rect must still permit conditional recovery
        // of our live text lease; actual leaf fit and geometry are checked separately.
        if(!Alive(text)||!Exact(text,"TMPro.TextMeshProUGUI"))return null;
        var owner=Parent<Gc.Character.CharacterTopController>(text);
        if(!Alive(owner)||!Exact(owner!,"Gc.Character.CharacterTopController"))return null;
        var button=owner!._unitEnhance;
        if(!Alive(button)||!Exact(button,"UnityEngine.UI.InertButton")||Ptr(button.textMeshProUgui)!=Ptr(text)||
            Ptr(text.transform.parent)!=Ptr(button.transform)||!button.transform.IsChildOf(owner.transform)||
            !CharacterEnhanceGeometryPolicy.MatchesPath(UnityNative.Nodes(text.transform,owner.transform)))return null;
        var container=button.GetComponent<RectTransform>();return Alive(container)?container:null;
    }
    bool TryCharacterEnhanceGeometry(Component value,UiBinding binding,TextMeshProUGUI text,
        out CharacterEnhanceGeometryIdentity? identity,bool diagnose=false,long ownerGeneration=0)
    {
        gate.Require();identity=null;
        if(!CharacterEnhanceGeometryPolicy.Supports(binding)||value is not Gc.Character.CharacterTopController owner||!Alive(owner)||!Alive(text))return false;
        uint proof=0;
        if(Exact(owner,"Gc.Character.CharacterTopController"))proof|=1;
        var button=owner._unitEnhance;
        if(Alive(button)&&Exact(button,"UnityEngine.UI.InertButton"))proof|=2;
        if(Alive(button)&&Ptr(button.textMeshProUgui)==Ptr(text))proof|=4;
        if(Alive(button)&&button.transform.IsChildOf(owner.transform))proof|=8;
        if(Alive(button)&&Ptr(text.transform.parent)==Ptr(button.transform))proof|=16;
        if(text.GetComponents<TextMeshProUGUI>().Length==1)proof|=32;
        bool current=owners.TryGetValue(Ptr(owner),out var captured)&&Current(captured!);
        if(current)proof|=64;
        var paths=ExactPaths(owner.transform,binding.Selector.Segments);
        if(paths.Count==1&&Ptr(paths[0])==Ptr(text.transform))proof|=128;
        if(Ptr(Parent<Gc.Character.CharacterTopController>(text))==Ptr(owner))proof|=256;
        var container=CharacterEnhanceContainer(text);if(Alive(container))proof|=512;
        var controller=owner.Controller;var scenes=Alive(controller)?controller!.createdScene:null;
        if(scenes!=null&&scenes.Length<=32&&CharacterEnhanceGeometryPolicy.SelectedOwner(Ptr(owner),controller!.currentSubSceneIndex,scenes.Select(x=>Ptr(x)).ToArray()))proof|=1024;
        long click=Alive(button)?Ptr(button.onClick):0;if(click!=0)proof|=2048;
        if(diagnose)
        {
            string id=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
            string code=$"CCGEO_{id}_O{ownerGeneration}_U{host.Snapshot.Generations.Ui}_P{proof:X}";
            if(characterEnhanceGeometryReports.Count<128&&characterEnhanceGeometryReports.Add(code))log.Record(code);
        }
        if(proof!=4095)return false;
        identity=new(Ptr(owner),owner.GetInstanceID(),Ptr(button),button.GetInstanceID(),Ptr(text),text.GetInstanceID(),
            Ptr(container),container!.GetInstanceID(),Ptr(controller),controller!.GetInstanceID(),click,captured!.Lifetime);
        return true;
    }
    bool CurrentCharacterEnhanceGeometry(GeometryTicket ticket)
    {
        gate.Require();
        if(ticket.CharacterEnhance==null||!ticket.OwnerCurrent()||!Alive(ticket.Text))return false;
        var owner=Parent<Gc.Character.CharacterTopController>(ticket.Text);
        return Alive(owner)&&TryCharacterEnhanceGeometry(owner!,ticket.Binding,ticket.Text,out var current)&&current==ticket.CharacterEnhance;
    }
    Func<bool> WithCharacterEnhanceSourceGuard(GeometryTicket? ticket,Func<bool> current)=>
        ticket is {CharacterEnhance:not null} guarded?()=>current()&&CurrentCharacterEnhanceGeometry(guarded):current;
}
