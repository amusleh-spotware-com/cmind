using Core.Options;
using Microsoft.Extensions.Options;

namespace Web.Registration;

/// <summary>
/// Fail-fast validation of <c>App:Registration</c> at startup: a self-registered user may never be granted
/// Owner/Admin, and the provisioning / CAPTCHA sub-features must carry the secrets they need. Runs via
/// <c>ValidateOnStart()</c> so a misconfigured deployment fails clearly at boot.
/// </summary>
public sealed class RegistrationOptionsValidator : IValidateOptions<AppOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options)
    {
        var reg = options.Registration;

        var role = reg.DefaultRole?.Trim() ?? string.Empty;
        if (!string.Equals(role, "User", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(role, "Viewer", StringComparison.OrdinalIgnoreCase))
            return ValidateOptionsResult.Fail(
                $"App:Registration:DefaultRole must be 'User' or 'Viewer' (was '{reg.DefaultRole}').");

        if (reg.Api.Enabled && string.IsNullOrWhiteSpace(reg.Api.Secret))
            return ValidateOptionsResult.Fail(
                "App:Registration:Api:Secret is required when App:Registration:Api:Enabled is true.");

        if (reg.Api.Enabled && reg.Api.Secret.Length < 24)
            return ValidateOptionsResult.Fail("App:Registration:Api:Secret must be at least 24 characters.");

        if (reg.Captcha.Enabled
            && (string.IsNullOrWhiteSpace(reg.Captcha.VerifyUrl) || string.IsNullOrWhiteSpace(reg.Captcha.Secret)))
            return ValidateOptionsResult.Fail(
                "App:Registration:Captcha requires VerifyUrl and Secret when Enabled is true.");

        return ValidateOptionsResult.Success;
    }
}
