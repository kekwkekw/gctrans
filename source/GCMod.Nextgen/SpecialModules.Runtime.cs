#nullable enable
namespace GCMod.Nextgen;

/// <summary>Configuration is managed authority; callbacks check it before reading native owners.</summary>
public static class SpecialDisplayPolicy
{
    public static bool AllowsWrites(bool translation,string? language)=>translation&&language is "ko" or "ko-ui3-common-poc";
    public static bool CanStartNativeWork(bool access,Func<bool>? enabled)=>access&&enabled?.Invoke()==true;
}
