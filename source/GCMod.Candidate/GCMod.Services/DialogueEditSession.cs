using System;

namespace GCMod.Services;

// Pure value state: preview never mutates the persisted configuration.
public sealed class DialogueEditSession
{
    public float Normal { get; private set; }
    public float Cg { get; private set; }
    public float SavedNormal { get; private set; }
    public float SavedCg { get; private set; }
    public bool Readability { get; private set; }
    public bool SavedReadability { get; private set; }
    public int FrameRate { get; private set; }
    public int SavedFrameRate { get; private set; }
    public bool IsDirty => Normal != SavedNormal || Cg != SavedCg || Readability != SavedReadability || FrameRate != SavedFrameRate;

    public DialogueEditSession(float normal, float cg, bool readability, int frameRate = 60) => Reload(normal, cg, readability, frameRate);
    public static float Normalize(float value) => float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
    public void Set(bool cg, float value)
    {
        if (cg) Cg = Normalize(value); else Normal = Normalize(value);
    }
    public void SetReadability(bool enabled) => Readability = enabled;
    public void SetFrameRate(int value)
    {
        if (value != 30 && value != 60) throw new ArgumentOutOfRangeException(nameof(value), "UI offers 30 or 60 FPS.");
        FrameRate = value;
    }
    public void Reload(float normal, float cg, bool readability, int frameRate = 60)
    {
        Normal = SavedNormal = Normalize(normal);
        Cg = SavedCg = Normalize(cg);
        Readability = SavedReadability = readability;
        // Preserve advanced cfg values until the user explicitly selects a UI choice.
        FrameRate = SavedFrameRate = frameRate;
    }
    public void Accept() { SavedNormal = Normal; SavedCg = Cg; SavedReadability = Readability; SavedFrameRate = FrameRate; }
    public void Discard() { Normal = SavedNormal; Cg = SavedCg; Readability = SavedReadability; FrameRate = SavedFrameRate; }
}
