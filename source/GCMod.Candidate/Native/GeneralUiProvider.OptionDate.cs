#nullable enable
namespace GCMod.Candidate.Native;

public sealed partial class GeneralUiProvider
{
    // Download.Setup can run while the tab is inactive. The existing finite owner
    // retry schedule also consumes this direct field when OnTabClick makes it visible.
    void DateCurrentOptionArea(Owner owner)
    {
        gate.Require();
        if (!Current(owner) || owner.Value is not Gc.Option.OptionPopup popup) return;
        var area = popup.downloadOptionArea;
        if (!Alive(area) || !Exact(area, "Gc.Option.DownloadOptionArea") || !area.gameObject.activeInHierarchy
            || !area.transform.IsChildOf(popup.transform)) return;
        var text = area.latestDateText;
        if (!Alive(text) || !text.enabled || !text.gameObject.activeInHierarchy || !text.transform.IsChildOf(area.transform)) return;
        long areaPointer = Ptr(area), textPointer = Ptr(text);
        int areaInstance = area.GetInstanceID(), textInstance = text.GetInstanceID();
        bool CurrentDate() => Current(owner) && Alive(area) && area.GetInstanceID() == areaInstance
            && Ptr(popup.downloadOptionArea) == areaPointer && Alive(text) && text.GetInstanceID() == textInstance
            && Ptr(area.latestDateText) == textPointer && text.transform.IsChildOf(area.transform)
            && area.transform.IsChildOf(popup.transform);
        // ApplyDirect still requires the already-enabled exact empty-date literal;
        // real timestamps/calendar formats remain outside this accepted renderer.
        ApplyDirect(popup, text, "option-date", "last-update-empty", text.text, CurrentDate, null, domain: 0);
    }
}
