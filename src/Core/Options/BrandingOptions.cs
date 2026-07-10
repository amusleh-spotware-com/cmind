using Core.Constants;

namespace Core.Options;

/// <summary>
/// White-label branding for a deployment, bound from <c>App:Branding</c>. Every value defaults to the stock
/// product identity, so an unconfigured deployment looks unchanged; a reseller overrides only what it needs.
/// Colours are validated when the theme is built (see the Web theme factory).
/// </summary>
public sealed record BrandingOptions
{
    public string ProductName { get; init; } = BrandingDefaults.ProductName;
    public string CompanyName { get; init; } = string.Empty;
    public string SupportUrl { get; init; } = string.Empty;
    public string Description { get; init; } = BrandingDefaults.Description;
    public string LogoUrl { get; init; } = string.Empty;
    public string FaviconUrl { get; init; } = BrandingDefaults.FaviconUrl;
    public string PrimaryColor { get; init; } = BrandingDefaults.PrimaryColor;
    public string SecondaryColor { get; init; } = BrandingDefaults.SecondaryColor;
    public string AppBarColor { get; init; } = BrandingDefaults.AppBarColor;
    public string BackgroundColor { get; init; } = BrandingDefaults.BackgroundColor;
    public string SurfaceColor { get; init; } = BrandingDefaults.SurfaceColor;
    public string SuccessColor { get; init; } = BrandingDefaults.SuccessColor;
    public string ErrorColor { get; init; } = BrandingDefaults.ErrorColor;
    public string WarningColor { get; init; } = BrandingDefaults.WarningColor;
    public string InfoColor { get; init; } = BrandingDefaults.InfoColor;
    public string CustomCss { get; init; } = string.Empty;
}
