#nullable enable
using GCMod.Nextgen;
using TMPro;
namespace GCMod.Candidate.Native;
/// Explicit public font publication; no parent hashes/private fields or historical receipts.
public sealed class NativeFontProvider(GameThreadGate gate,FontService service):IFontGlyphProvider,IDisposable {
 TMP_FontAsset? medium; long generation;
 public FontService Service=>service;
 public TMP_FontAsset? Medium=>gate.CanAccess&&UnityNative.Alive(medium)?medium:null;
 public TMP_FontAsset? GameFont {get;private set;}
 public FontDonorHandle? Current=>service.Current;
 public bool TryPublish(TMP_FontAsset? donor){gate.Require();if(!UnityNative.Alive(donor)||donor!.name!=FontService.ApprovedProfile||donor.m_AtlasPopulationMode!=AtlasPopulationMode.Static)return false;
  if(UnityNative.Ptr(donor)==UnityNative.Ptr(medium)&&service.Current?.InstanceId==donor.GetInstanceID())return true;
  if(generation>0)service.Invalidate(generation);medium=donor;generation=gate.NextLifetime();service.PublishMediumDonor(new(FontService.ApprovedProfile,(ulong)UnityNative.Ptr(donor),donor.GetInstanceID(),generation),this);return true;}
 public void ObserveGameFont(TMP_FontAsset? font){gate.Require();if(UnityNative.Alive(font)&&font!.name!=FontService.ApprovedProfile)GameFont=font;}
 public bool HasGlyph(FontDonorHandle donor,int scalar){gate.Require();return donor==Current&&UnityNative.Alive(medium)&&medium!.name==FontService.ApprovedProfile&&medium.m_AtlasPopulationMode==AtlasPopulationMode.Static&&medium.GetInstanceID()==donor.InstanceId&&medium.characterLookupTable!=null&&medium.characterLookupTable.ContainsKey((uint)scalar);}
 public bool Glyphs(TMP_FontAsset? font,string value){gate.Require();if(!UnityNative.Alive(font))return false;return value.EnumerateRunes().All(r=>System.Text.Rune.IsControl(r)||Glyph(font!,r.Value,new HashSet<long>(),0));}
 bool Glyph(TMP_FontAsset font,int scalar,HashSet<long> seen,int depth){if(depth>4||seen.Count>=32||!UnityNative.Alive(font)||!seen.Add(UnityNative.Ptr(font)))return false;if(font.characterLookupTable!=null&&font.characterLookupTable.ContainsKey((uint)scalar))return true;var fallback=font.fallbackFontAssetTable;if(fallback!=null&&fallback.Count<=32)foreach(var child in fallback)if(UnityNative.Alive(child)&&Glyph(child,scalar,seen,depth+1))return true;var global=TMP_Settings.fallbackFontAssets;return global!=null&&global.Count<=32&&Current!=null&&global.ToArray().Any(f=>UnityNative.Ptr(f)==UnityNative.Ptr(Medium))&&HasGlyph(Current,scalar);}

 public void Tick(){gate.Require();if(!ReferenceEquals(medium,null)&&(!UnityNative.Alive(medium)||medium!.name!=FontService.ApprovedProfile||medium.m_AtlasPopulationMode!=AtlasPopulationMode.Static)){service.Invalidate(generation);medium=null;}}
 public void Dispose(){gate.Require();if(generation>0)service.Invalidate(generation);medium=null;GameFont=null;}
}
