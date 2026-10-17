#nullable enable
namespace GCMod.Nextgen;

/// The recovered Character menu enhancement caption keeps its own native button
/// layout. Animation of separate native sibling transforms is not its text layout.
public static class CharacterEnhanceGeometryPolicy
{
    static readonly string[] CaptionPath={"Animation_Main","Frame_Character","Unit","BtUnitEnhance","Text"};
    public static bool MatchesPath(IEnumerable<string> path)=>path.SequenceEqual(CaptionPath,StringComparer.Ordinal);
    public static bool Supports(UiBinding binding)=>binding.Enabled&&binding.AdapterId=="ui.chrome.v1"&&
        binding.SourceIdentity.Kind=="current-model"&&binding.SourceIdentity.ExactJp=="強化"&&
        binding.Selector.ScopeKey=="gc.character.charactertopcontroller"&&binding.Selector.SlotKey=="artist_enhance"&&
        binding.Selector.Type=="caption"&&binding.Selector.Lifetime=="owner"&&MatchesPath(binding.Selector.Segments)&&binding.Producer==null;

    public static bool SelectedOwner(long owner,int current,IReadOnlyList<long> scenes)=>owner!=0&&
        scenes.Count is >0 and <=32&&current>=0&&current<scenes.Count&&scenes[current]==owner&&scenes.Count(x=>x==owner)==1;
}
