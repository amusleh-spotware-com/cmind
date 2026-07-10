using Core.Branding;
using Core.Domain;
using Core.Options;
using Microsoft.Extensions.Options;

namespace Web.Branding;

/// <summary>
/// Fail-fast validation of white-label branding at startup: every colour must be a valid hex value, and the
/// injected custom CSS must not contain angle brackets (which could break out of the <c>&lt;style&gt;</c> tag).
/// Runs at boot via <c>ValidateOnStart()</c> so a misconfigured deployment fails clearly instead of throwing
/// on the first rendered request.
/// </summary>
public sealed class BrandingOptionsValidator : IValidateOptions<AppOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options)
    {
        var branding = options.Branding;
        var colors = new[]
        {
            branding.PrimaryColor, branding.SecondaryColor, branding.AppBarColor, branding.BackgroundColor,
            branding.SurfaceColor, branding.SuccessColor, branding.ErrorColor, branding.WarningColor,
            branding.InfoColor
        };

        foreach (var color in colors)
        {
            try
            {
                _ = new HexColor(color);
            }
            catch (DomainException)
            {
                return ValidateOptionsResult.Fail($"Invalid App:Branding colour '{color}'.");
            }
        }

        if (branding.CustomCss.Contains('<') || branding.CustomCss.Contains('>'))
            return ValidateOptionsResult.Fail("App:Branding:CustomCss must not contain '<' or '>'.");

        return ValidateOptionsResult.Success;
    }
}
