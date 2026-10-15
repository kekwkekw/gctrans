#nullable enable
using System.Text;

namespace GCMod.Nextgen;

public sealed record FontDonorHandle(string Profile, ulong NativeHandle, long InstanceId, long Generation);
public interface IFontGlyphProvider { bool HasGlyph(FontDonorHandle donor, int unicodeScalar); }

/// <summary>Explicit donor ownership API, with no private-field reflection or bundle loading.</summary>
public sealed class FontService
{
    public const string ApprovedProfile = "RHR_Medium_GFSAFE_ADH";
    readonly object gate = new();
    FontDonorHandle? donor;
    IFontGlyphProvider? glyphs;
    long lastGeneration;
    public FontDonorHandle? Current { get { lock (gate) return donor; } }
    public void PublishMediumDonor(FontDonorHandle handle, IFontGlyphProvider provider)
    {
        if (handle.Profile != ApprovedProfile || handle.NativeHandle == 0 || handle.Generation <= 0) throw new ArgumentException("UNAPPROVED_FONT_DONOR");
        lock (gate)
        {
            // Retain the generation barrier after invalidation so a late callback cannot
            // republish a disposed donor from the previous scene/font lifetime.
            if (handle.Generation <= lastGeneration) throw new InvalidOperationException("STALE_FONT_GENERATION");
            lastGeneration = handle.Generation; donor = handle; glyphs = provider;
        }
    }
    public void Invalidate(long generation)
    {
        if (generation <= 0) throw new ArgumentOutOfRangeException(nameof(generation));
        lock (gate)
        {
            // Invalidation may arrive before the asynchronous first publication.
            lastGeneration = Math.Max(lastGeneration, generation);
            if (donor?.Generation == generation) { donor = null; glyphs = null; }
        }
    }
    public bool Supports(string text, FontDonorHandle expected)
    {
        lock (gate)
        {
            if (donor != expected || glyphs == null) return false;
            try { return text.EnumerateRunes().All(r => Rune.IsControl(r) || glyphs.HasGlyph(donor, r.Value)); }
            catch { return false; }
        }
    }
}
