namespace GCMod.Services;

/// <summary>与翻译项目 scripts/build.py 一致的嵌套翻译表哈希。</summary>
public static class TranslationHash
{
    public static string Compute(string json) => GCMod.Nextgen.LegacySemanticHash.Compute(json);
}
