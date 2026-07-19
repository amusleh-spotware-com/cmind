using Core.Features;
using Core.Options;

namespace Core.Cot;

/// <summary>
/// The single source of truth for whether the Commitment of Traders feature is on, composing the two-tier
/// gate: the white-label hard gate (<see cref="BrandingOptions.EnableCot"/>, Tier 2) AND the runtime feature
/// toggle (<see cref="FeatureFlag.Cot"/>, Tier 1). Nav, endpoints, API auth, MCP tools and worker
/// registration all read this so the two tiers can never disagree. Unlike the economic calendar, COT data
/// comes from a keyless public source, so there is no data-source key gate — enabled means visible.
/// </summary>
public static class CotEnablement
{
    /// <summary>White-label off ⇒ invisible regardless of the runtime toggle; else the operator toggle decides.</summary>
    public static bool IsEnabled(bool brandingEnabled, bool featureToggleEnabled)
        => brandingEnabled && featureToggleEnabled;

    /// <summary>Resolves the effective state from the branding options and the feature gate.</summary>
    public static bool IsEnabled(BrandingOptions branding, IFeatureGate featureGate)
        => IsEnabled(branding.EnableCot, featureGate.IsEnabled(FeatureFlag.Cot));

    /// <summary>Whether the runtime toggle should even be shown — only when the white-label gate permits it.</summary>
    public static bool IsRuntimeToggleVisible(BrandingOptions branding) => branding.EnableCot;

    /// <summary>Whether the COT feature should be surfaced in the UI (nav + page data) and its APIs answer.</summary>
    public static bool IsVisible(BrandingOptions branding, IFeatureGate featureGate)
        => IsEnabled(branding, featureGate);
}
