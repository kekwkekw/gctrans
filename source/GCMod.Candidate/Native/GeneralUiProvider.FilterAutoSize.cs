#nullable enable
using System.Text.Json;
using GCMod.Nextgen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GCMod.Candidate.Native;
public sealed partial class GeneralUiProvider
{
    sealed record FilterAutoSizeIdentity(long Group, int GroupInstance, long Popup, int PopupInstance,
        long Setting, string Heading, long Entries, long Toggles, long Toggle, int ToggleInstance, int Row, int Index, int Value);
    sealed record FilterAutoSizeState(FilterAutoSizeIdentity Identity, NativeAutoSizeConfiguration Configuration, float BaselineSize);
    readonly HashSet<string> filterAutoSizeReports = new(StringComparer.Ordinal);
    static NativeAutoSizeProfile ReadAutoSize(TextMeshProUGUI text) => new(text.enableAutoSizing, text.fontSizeMin, text.fontSizeMax, text.fontSize);
    bool TryFilterAutoSizeIdentity(Component value, UiBinding binding, TextMeshProUGUI text, out FilterAutoSizeIdentity? identity)
    {
        gate.Require(); identity = null;
        if (!FilterOptionAutoSizePolicy.Supports(binding) || value is not Gc.ToggleFilterGroup group
            || !Alive(group) || !Exact(group,"Gc.ToggleFilterGroup") || !Alive(text) || !Exact(text,"TMPro.TextMeshProUGUI")
            || !text.transform.IsChildOf(group.transform) || text.GetComponents<TextMeshProUGUI>().Length != 1
            || Ptr(Parent<Gc.ToggleFilterGroup>(text)) != Ptr(group)) return false;
        var popup=Parent<Gc.FilterPopup>(group); var setting=group._setting; var toggles=group._toggleList;
        var toggle=Parent<InertToggle>(text); var values=setting?._NameAndValues_k__BackingField;
        if (!Alive(popup) || !Exact(popup!,"Gc.FilterPopup") || setting==null || values==null || values.Length>64
            || toggles==null || toggles.Count>64 || !Alive(toggle) || !Exact(toggle!,"UnityEngine.UI.InertToggle")
            || Ptr(toggle!.text)!=Ptr(text) || !text.transform.IsChildOf(toggle.transform)
            || !toggle.transform.IsChildOf(group.transform) || toggle._Value_k__BackingField!=binding.Selector.SourceValue) return false;
        var dictionary=popup!._filterGroupDict;
        if(dictionary==null || dictionary.Count>32) return false;
        int domains=0,domain=-1;
        for(int i=0;i<22;i++)if(dictionary.TryGetValue((Gc.FilterType)i,out var member)&&Ptr(member)==Ptr(group)){domains++;domain=i;}
        if(domains!=1||domain!=6)return false;
        int row=-1,rows=0,index=-1,members=0,sameValue=0;
        for(int i=0;i<values.Length;i++)if(values[i]._Value_k__BackingField==binding.Selector.SourceValue)
        { rows++;row=i;if(values[i]._Name_k__BackingField!=binding.SourceIdentity.ExactJp)return false; }
        for(int i=0;i<toggles.Count;i++)
        { var candidate=toggles[i];if(!Alive(candidate))continue;if(Ptr(candidate)==Ptr(toggle)){members++;index=i;}
          if(candidate._Value_k__BackingField==binding.Selector.SourceValue)sameValue++; }
        if(rows!=1||members!=1||sameValue!=1)return false;
        identity=new(Ptr(group),group.GetInstanceID(),Ptr(popup),popup.GetInstanceID(),Ptr(setting),setting._Title_k__BackingField??"",
            Ptr(values),Ptr(toggles),Ptr(toggle),toggle.GetInstanceID(),row,index,binding.Selector.SourceValue!.Value);
        return true;
    }
    FilterAutoSizeState? CaptureFilterAutoSize(Owner owner, UiBinding binding, TextMeshProUGUI text)
    {
        gate.Require(); if(!FilterOptionAutoSizePolicy.Supports(binding))return null;
        bool typed=TryFilterAutoSizeIdentity(owner.Value,binding,text,out var identity);
        var profile=ReadAutoSize(text);var rect=text.rectTransform.rect;
        bool positive=float.IsFinite(rect.width)&&float.IsFinite(rect.height)&&rect.width>0&&rect.height>0;
        var configuration=typed&&positive?FilterOptionAutoSizePolicy.Capture(profile):null;
        RecordFilterAutoSize(binding,owner.Lifetime,text,profile.Size,profile,"PREPARE",typed,configuration!=null);
        return configuration!=null ? new(identity!,configuration,profile.Size) : null;
    }
    bool CurrentFilterAutoSizeIdentity(GeometryTicket ticket)
    {
        gate.Require();
        if(ticket.AutoSize==null||!ticket.OwnerCurrent()||!Alive(ticket.Text))return false;
        var group=Parent<Gc.ToggleFilterGroup>(ticket.Text);
        return Alive(group)&&TryFilterAutoSizeIdentity(group!,ticket.Binding,ticket.Text,out var current)&&current==ticket.AutoSize.Identity;
    }
    Func<bool> WithFilterAutoSizeSourceGuard(GeometryTicket? ticket, Func<bool> current)
        => ticket is {AutoSize:not null} guarded ? ()=>current()&&CurrentFilterAutoSizeIdentity(guarded) : current;
    void RecordFilterAutoSize(UiBinding binding,long lifetime,TextMeshProUGUI text,float baseline,NativeAutoSizeProfile current,string phase,bool typed,bool cooperating)
    {
        gate.Require();
        string id=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
        string key=$"{id}_O{lifetime}_U{host.Snapshot.Generations.Ui}_{phase}";
        if(filterAutoSizeReports.Count>=512||!filterAutoSizeReports.Add(key))return;
        static int M(float n)=>float.IsFinite(n)?(int)Math.Clamp(n*100,-99999,99999):99999;
        log.Record($"GEOSIZE_{key}_B{M(baseline)}_C{M(current.Size)}_E{(current.Enabled?1:0)}_L{M(current.Minimum)}_H{M(current.Maximum)}_T{(typed?1:0)}_P{(cooperating?1:0)}");
        if(!typed||!cooperating)
        {
            var components=text.GetComponents<Component>();
            log.Record($"GEOSIZEC_{id}_O{lifetime}_N{Math.Min(components.Length,9)}");
            for(int i=0;i<Math.Min(components.Length,8);i++)
            {
                string name=UnityNative.Class(components[i]);
                string code=new(name.ToUpperInvariant().Select(c=>c is >= 'A' and <= 'Z' or >= '0' and <= '9'?c:'_').Take(40).ToArray());
                log.Record($"GEOSIZEC_{id}_O{lifetime}_I{i}_{code}");
            }
        }
    }
    void RecordFilterAutoSizeFailure(GeometryTicket ticket)
    {
        gate.Require();
        if(!FilterOptionAutoSizePolicy.Supports(ticket.Binding)||!ticket.OwnerCurrent()||!Alive(ticket.Text)
            ||ticket.Text.GetInstanceID()!=ticket.Instance||!ticket.SourceCurrent())return;
        float baseline=ticket.AutoSize?.BaselineSize??0;
        if(ticket.AutoSize==null)
        { using var original=JsonDocument.Parse(ticket.Display);if(original.RootElement.TryGetProperty("size",out var size)&&size.ValueKind==JsonValueKind.Number)baseline=size.GetSingle(); }
        var profile=ReadAutoSize(ticket.Text);
        RecordFilterAutoSize(ticket.Binding,ticket.OwnerGeneration,ticket.Text,baseline,profile,"FAILED",
            TryFilterAutoSizeIdentity(Parent<Gc.ToggleFilterGroup>(ticket.Text)!,ticket.Binding,ticket.Text,out _),
            ticket.AutoSize!=null&&FilterOptionAutoSizePolicy.Matches(ticket.AutoSize.Configuration,profile));
    }
}
