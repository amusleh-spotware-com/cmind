using System.Globalization;
using System.Reflection;
using Core.Nodes;
using Core.Options;

namespace Core.WhiteLabel;

/// <summary>
/// Pure application of owner overrides onto <see cref="AppOptions"/>, and reflection-based reading of a
/// white-label option's current value. Kept infra-free so it unit-tests without a database. Parsing is
/// defensive: a malformed override keeps the baseline value (never throws) so a bad row can never break the
/// whole options pipeline — validation happens at write time.
/// </summary>
public static class WhiteLabelOverlay
{
    /// <summary>
    /// Returns a new <see cref="AppOptions"/> with the given decrypted overrides applied. <paramref name="raw"/>
    /// is keyed by <see cref="WhiteLabelOption.Key"/> and contains only non-feature-flag overrides (secrets
    /// already decrypted). Categories with no override are rebuilt from the same baseline values (no-op).
    /// </summary>
    public static AppOptions Apply(AppOptions baseline, IReadOnlyDictionary<string, string> raw)
    {
        if (raw.Count == 0) return baseline;

        string? Ov(string key) => raw.TryGetValue(key, out var v) ? v : null;

        var b = baseline.Branding with
        {
            ProductName = Ov("branding.productName") ?? baseline.Branding.ProductName,
            CompanyName = Ov("branding.companyName") ?? baseline.Branding.CompanyName,
            SupportUrl = Ov("branding.supportUrl") ?? baseline.Branding.SupportUrl,
            Description = Ov("branding.description") ?? baseline.Branding.Description,
            LogoUrl = Ov("branding.logoUrl") ?? baseline.Branding.LogoUrl,
            FaviconUrl = Ov("branding.faviconUrl") ?? baseline.Branding.FaviconUrl,
            CustomCss = Ov("branding.customCss") ?? baseline.Branding.CustomCss,
            PrimaryColor = Ov("branding.primaryColor") ?? baseline.Branding.PrimaryColor,
            SecondaryColor = Ov("branding.secondaryColor") ?? baseline.Branding.SecondaryColor,
            AppBarColor = Ov("branding.appBarColor") ?? baseline.Branding.AppBarColor,
            BackgroundColor = Ov("branding.backgroundColor") ?? baseline.Branding.BackgroundColor,
            SurfaceColor = Ov("branding.surfaceColor") ?? baseline.Branding.SurfaceColor,
            SuccessColor = Ov("branding.successColor") ?? baseline.Branding.SuccessColor,
            ErrorColor = Ov("branding.errorColor") ?? baseline.Branding.ErrorColor,
            WarningColor = Ov("branding.warningColor") ?? baseline.Branding.WarningColor,
            InfoColor = Ov("branding.infoColor") ?? baseline.Branding.InfoColor,
            ShowSiteLink = ParseBool(Ov("branding.showSiteLink"), baseline.Branding.ShowSiteLink),
            RequireMfa = ParseBool(Ov("branding.requireMfa"), baseline.Branding.RequireMfa),
            AllowBuiltInAi = ParseBool(Ov("branding.allowBuiltInAi"), baseline.Branding.AllowBuiltInAi),
            AllowLocalProviders = ParseBool(Ov("branding.allowLocalProviders"), baseline.Branding.AllowLocalProviders),
            AllowedAiProviderKinds = ParseList(Ov("branding.allowedAiProviderKinds"), baseline.Branding.AllowedAiProviderKinds),
            EnableEconomicCalendar = ParseBool(Ov("branding.enableEconomicCalendar"), baseline.Branding.EnableEconomicCalendar),
            RestrictNodesToOwner = ParseBool(Ov("branding.restrictNodesToOwner"), baseline.Branding.RestrictNodesToOwner),
            NodesUi = ParseEnum(Ov("branding.nodesUi"), baseline.Branding.NodesUi)
        };

        var accounts = baseline.Accounts with
        {
            AllowedBrokers = ParseList(Ov("accounts.allowedBrokers"), baseline.Accounts.AllowedBrokers),
            BrokerProbeTimeout = ParseTimeSpan(Ov("accounts.brokerProbeTimeout"), baseline.Accounts.BrokerProbeTimeout),
            BrokerProbeAlgoPath = Ov("accounts.brokerProbeAlgoPath") ?? baseline.Accounts.BrokerProbeAlgoPath
        };

        var registration = baseline.Registration with
        {
            Enabled = ParseBool(Ov("registration.enabled"), baseline.Registration.Enabled),
            Mode = ParseEnum(Ov("registration.mode"), baseline.Registration.Mode),
            DefaultRole = Ov("registration.defaultRole") ?? baseline.Registration.DefaultRole,
            RequireTermsAcceptance = ParseBool(Ov("registration.requireTermsAcceptance"), baseline.Registration.RequireTermsAcceptance),
            AllowedEmailDomains = ParseList(Ov("registration.allowedEmailDomains"), baseline.Registration.AllowedEmailDomains),
            BlockDisposableEmail = ParseBool(Ov("registration.blockDisposableEmail"), baseline.Registration.BlockDisposableEmail),
            TokenLifetime = ParseTimeSpan(Ov("registration.tokenLifetime"), baseline.Registration.TokenLifetime),
            Captcha = baseline.Registration.Captcha with
            {
                Enabled = ParseBool(Ov("registration.captcha.enabled"), baseline.Registration.Captcha.Enabled),
                VerifyUrl = Ov("registration.captcha.verifyUrl") ?? baseline.Registration.Captcha.VerifyUrl,
                SiteKey = Ov("registration.captcha.siteKey") ?? baseline.Registration.Captcha.SiteKey,
                Secret = Ov("registration.captcha.secret") ?? baseline.Registration.Captcha.Secret
            },
            Api = baseline.Registration.Api with
            {
                Enabled = ParseBool(Ov("registration.api.enabled"), baseline.Registration.Api.Enabled),
                Secret = Ov("registration.api.secret") ?? baseline.Registration.Api.Secret,
                ActivateImmediately = ParseBool(Ov("registration.api.activateImmediately"), baseline.Registration.Api.ActivateImmediately),
                InviteMustChangePassword = ParseBool(Ov("registration.api.inviteMustChangePassword"), baseline.Registration.Api.InviteMustChangePassword)
            },
            Attributes = baseline.Registration.Attributes with
            {
                FullName = ParseEnum(Ov("registration.attributes.fullName"), baseline.Registration.Attributes.FullName),
                DisplayName = ParseEnum(Ov("registration.attributes.displayName"), baseline.Registration.Attributes.DisplayName),
                Country = ParseEnum(Ov("registration.attributes.country"), baseline.Registration.Attributes.Country),
                Phone = ParseEnum(Ov("registration.attributes.phone"), baseline.Registration.Attributes.Phone),
                Company = ParseEnum(Ov("registration.attributes.company"), baseline.Registration.Attributes.Company),
                Locale = ParseEnum(Ov("registration.attributes.locale"), baseline.Registration.Attributes.Locale),
                MarketingOptIn = ParseEnum(Ov("registration.attributes.marketingOptIn"), baseline.Registration.Attributes.MarketingOptIn),
                AgeConfirmation = ParseEnum(Ov("registration.attributes.ageConfirmation"), baseline.Registration.Attributes.AgeConfirmation)
            }
        };

        var email = baseline.Email with
        {
            Host = Ov("email.host") ?? baseline.Email.Host,
            Port = ParseInt(Ov("email.port"), baseline.Email.Port),
            UseStartTls = ParseBool(Ov("email.useStartTls"), baseline.Email.UseStartTls),
            Username = Ov("email.username") ?? baseline.Email.Username,
            Password = Ov("email.password") ?? baseline.Email.Password,
            FromAddress = Ov("email.fromAddress") ?? baseline.Email.FromAddress,
            FromName = Ov("email.fromName") ?? baseline.Email.FromName
        };

        var propFirm = baseline.PropFirm with
        {
            DrawdownWarnThresholdPercent = ParseDouble(Ov("propFirm.drawdownWarnThresholdPercent"), baseline.PropFirm.DrawdownWarnThresholdPercent)
        };

        var openApi = baseline.OpenApi with
        {
            PublicBaseUrl = Ov("openApi.publicBaseUrl") ?? baseline.OpenApi.PublicBaseUrl
        };

        var ai = baseline.Ai with
        {
            BuiltIn = baseline.Ai.BuiltIn with
            {
                Enabled = ParseBool(Ov("ai.builtIn.enabled"), baseline.Ai.BuiltIn.Enabled)
            }
        };

        return baseline with
        {
            Branding = b,
            Accounts = accounts,
            Registration = registration,
            Email = email,
            PropFirm = propFirm,
            OpenApi = openApi,
            Ai = ai
        };
    }

    /// <summary>Reads the current value of an option from <paramref name="options"/> as an invariant string.</summary>
    public static string? ReadRaw(AppOptions options, WhiteLabelOption option)
    {
        object? current = options;
        foreach (var part in option.PropertyPath.Split('.'))
        {
            if (current is null) return null;
            var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance);
            if (prop is null) return null;
            current = prop.GetValue(current);
        }
        return Format(current);
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        bool b => b ? bool.TrueString : bool.FalseString,
        string s => s,
        TimeSpan t => t.ToString("c", CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString(CultureInfo.InvariantCulture),
        Enum e => e.ToString(),
        IEnumerable<string> list => string.Join(", ", list),
        _ => value.ToString()
    };

    private static bool ParseBool(string? raw, bool fallback) =>
        bool.TryParse(raw, out var v) ? v : fallback;

    private static int ParseInt(string? raw, int fallback) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static double ParseDouble(string? raw, double fallback) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static TimeSpan ParseTimeSpan(string? raw, TimeSpan fallback) =>
        TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static TEnum ParseEnum<TEnum>(string? raw, TEnum fallback) where TEnum : struct, Enum =>
        System.Enum.TryParse<TEnum>(raw, ignoreCase: true, out var v) && System.Enum.IsDefined(v) ? v : fallback;

    private static IReadOnlyList<string> ParseList(string? raw, IReadOnlyList<string> fallback)
    {
        if (raw is null) return fallback;
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
