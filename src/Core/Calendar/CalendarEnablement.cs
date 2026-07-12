using Core.Features;
using Core.Options;

namespace Core.Calendar;

/// <summary>
/// The single source of truth for whether the economic calendar is on, composing the two-tier gate:
/// the white-label hard gate (<see cref="BrandingOptions.EnableEconomicCalendar"/>, Tier 2) AND the runtime
/// feature toggle (<see cref="FeatureFlag.EconomicCalendar"/>, Tier 1). Nav, endpoints, API auth, MCP tools
/// and worker registration all read this so the two tiers can never disagree.
/// </summary>
public static class CalendarEnablement
{
    /// <summary>White-label off ⇒ invisible regardless of the runtime toggle; else the operator toggle decides.</summary>
    public static bool IsEnabled(bool brandingEnabled, bool featureToggleEnabled)
        => brandingEnabled && featureToggleEnabled;

    /// <summary>Resolves the effective state from the branding options and the feature gate.</summary>
    public static bool IsEnabled(BrandingOptions branding, IFeatureGate featureGate)
        => IsEnabled(branding.EnableEconomicCalendar, featureGate.IsEnabled(FeatureFlag.EconomicCalendar));

    /// <summary>Whether the runtime toggle should even be shown — only when the white-label gate permits it.</summary>
    public static bool IsRuntimeToggleVisible(BrandingOptions branding) => branding.EnableEconomicCalendar;
}
