#nullable enable
namespace GCMod.Nextgen;

public sealed record NativeAutoSizeProfile(bool Enabled, float Minimum, float Maximum, float Size);
public sealed record NativeAutoSizeConfiguration(float Minimum, float Maximum);
/// Only existing owner-scoped category option bindings may cooperate with a proven native TMP driver.
public static class FilterOptionAutoSizePolicy
{
    public static bool Supports(UiBinding binding) => binding.Enabled && binding.AdapterId == "common.model-label.v1"
        && binding.SourceIdentity.Kind == "current-model" && binding.Selector.ScopeKey == "current-filter"
        && binding.Selector.SlotKey == "filter_option" && binding.Selector.Type == "model"
        && binding.Selector.Lifetime == "owner" && binding.Selector.Domain == 6
        && binding.Selector.SourceValue.HasValue && binding.Selector.SourceId == null;
    public static bool WithinRange(NativeAutoSizeProfile profile) => profile.Enabled
        && new[] { profile.Minimum, profile.Maximum, profile.Size }.All(float.IsFinite)
        && profile.Minimum > 0 && profile.Minimum <= profile.Maximum && profile.Maximum <= 512
        && profile.Size >= profile.Minimum - .001f && profile.Size <= profile.Maximum + .001f;
    public static NativeAutoSizeConfiguration? Capture(NativeAutoSizeProfile profile)
        => WithinRange(profile) ? new(profile.Minimum, profile.Maximum) : null;
    public static bool ConfigurationMatches(NativeAutoSizeConfiguration expected, NativeAutoSizeProfile current)
        => current.Enabled && current.Minimum == expected.Minimum && current.Maximum == expected.Maximum;
    public static bool Matches(NativeAutoSizeConfiguration expected, NativeAutoSizeProfile current)
        => ConfigurationMatches(expected, current) && WithinRange(current);
}
