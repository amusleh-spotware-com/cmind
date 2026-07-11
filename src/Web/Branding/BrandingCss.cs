using System.Globalization;
using System.Text;
using Core.Branding;
using Core.Options;

namespace Web.Branding;

/// <summary>
/// Emits the white-label design tokens as CSS custom properties on <c>:root</c>. Both MudBlazor (via the
/// theme factory) and the app's own stylesheet read the same branding values, so a reseller's palette flows
/// everywhere without any hard-coded colour in a component. Neutral surface tones keep the stock defaults
/// declared in <c>site.css</c> and are only overridden here for the branded roles.
/// </summary>
public static class BrandingCss
{
    public static string BuildRootVariables(BrandingOptions branding)
    {
        var primary = new HexColor(branding.PrimaryColor).Value;
        var secondary = new HexColor(branding.SecondaryColor).Value;
        var background = new HexColor(branding.BackgroundColor).Value;
        var surface = new HexColor(branding.SurfaceColor).Value;
        var appBar = new HexColor(branding.AppBarColor).Value;
        var success = new HexColor(branding.SuccessColor).Value;
        var error = new HexColor(branding.ErrorColor).Value;
        var warning = new HexColor(branding.WarningColor).Value;
        var info = new HexColor(branding.InfoColor).Value;

        var builder = new StringBuilder(":root{", 480);
        Append(builder, "--app-primary", primary);
        Append(builder, "--app-primary-hover", $"color-mix(in srgb, {primary} 82%, #ffffff)");
        Append(builder, "--app-primary-contrast", "#ffffff");
        Append(builder, "--app-secondary", secondary);
        Append(builder, "--app-bg", background);
        Append(builder, "--app-surface", surface);
        Append(builder, "--app-appbar", appBar);
        Append(builder, "--app-success", success);
        Append(builder, "--app-error", error);
        Append(builder, "--app-warning", warning);
        Append(builder, "--app-info", info);
        builder.Append('}');
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string name, string value) =>
        builder.Append(CultureInfo.InvariantCulture, $"{name}:{value};");
}
