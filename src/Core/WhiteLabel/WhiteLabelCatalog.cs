using Core.Features;
using Core.Nodes;
using Core.Options;

namespace Core.WhiteLabel;

/// <summary>
/// AppSetting key namespace + cache identity for owner-set white-label overrides. A row of the form
/// <c>whitelabel.&lt;Key&gt;</c> stores the raw override value (ciphertext for secrets); absence means "use the
/// deployment configuration baseline". Mirrors <see cref="Core.Constants.FeatureSettings"/>.
/// </summary>
public static class WhiteLabelSettingsKeys
{
    public const string OverrideKeyPrefix = "whitelabel.";
    public const string OverrideCacheKey = "whitelabel.overrides";
    public static readonly TimeSpan OverrideCacheTtl = TimeSpan.FromSeconds(10);

    public static string OverrideKey(string optionKey) => OverrideKeyPrefix + optionKey;
}

/// <summary>
/// The single registry of every white-label option a deployment can set through configuration and an owner
/// can override at runtime. New white-label option ⇒ add it here in the same commit (enforced by
/// <c>WhiteLabelCatalogParityTests</c>). See the "white-label options in sync" mandate in CLAUDE.md.
/// </summary>
public static class WhiteLabelCatalog
{
    public static IReadOnlyList<WhiteLabelOption> All { get; } = Build();

    /// <summary>
    /// White-label options-record properties deliberately NOT owner-tunable (operational-only), each with a
    /// reason. The parity test asserts every reflected property is either in <see cref="All"/> or here.
    /// </summary>
    public static IReadOnlyDictionary<string, string> IntentionallyExcluded { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly Dictionary<string, WhiteLabelOption> ByKey =
        All.ToDictionary(o => o.Key, StringComparer.Ordinal);

    public static bool TryGet(string key, out WhiteLabelOption option) => ByKey.TryGetValue(key, out option!);

    private static List<WhiteLabelOption> Build()
    {
        var list = new List<WhiteLabelOption>
        {
            // ---- Branding (identity) ----
            Str("branding.productName", "Branding.ProductName", WhiteLabelCategory.Branding, "Product name",
                "The product name shown in the title bar, PWA manifest and emails."),
            Str("branding.companyName", "Branding.CompanyName", WhiteLabelCategory.Branding, "Company name",
                "Your company/operator name; shown where the product credits an operator."),
            Str("branding.supportUrl", "Branding.SupportUrl", WhiteLabelCategory.Branding, "Support URL",
                "Link users follow for support. Empty hides the support link."),
            Multi("branding.description", "Branding.Description", WhiteLabelCategory.Branding, "Description",
                "Meta description / tagline used for SEO and the PWA."),
            Str("branding.logoUrl", "Branding.LogoUrl", WhiteLabelCategory.Branding, "Logo URL",
                "URL of the logo image. Empty uses the built-in mark."),
            Str("branding.faviconUrl", "Branding.FaviconUrl", WhiteLabelCategory.Branding, "Favicon URL",
                "URL/path of the browser tab icon."),
            Multi("branding.customCss", "Branding.CustomCss", WhiteLabelCategory.Branding, "Custom CSS",
                "Extra CSS injected into every page for fine-grained white-label tweaks."),
            Bool("branding.showSiteLink", "Branding.ShowSiteLink", WhiteLabelCategory.Branding, "Show \"Powered by\" link",
                "Whether the dashboard shows the credit link back to the project site."),
            Bool("branding.requireMfa", "Branding.RequireMfa", WhiteLabelCategory.Branding, "Require MFA",
                "Force every user to set up two-factor authentication before using the app."),
            Bool("branding.restrictNodesToOwner", "Branding.RestrictNodesToOwner", WhiteLabelCategory.Branding,
                "Restrict nodes to owner",
                "When on, only the owner may see and manage nodes; otherwise admin-or-above staff can."),
            Enum("branding.nodesUi", "Branding.NodesUi", WhiteLabelCategory.Branding, "Nodes UI mode",
                typeof(NodesUiMode),
                "How much of the Nodes surface is exposed: Full (manage), Monitor (read-only), or Hidden."),
            Str("branding.defaultTimeZone", "Branding.DefaultTimeZone", WhiteLabelCategory.Branding,
                "Default time zone",
                "Canonical IANA time zone new users see until they choose their own or their browser zone is detected."),

            // ---- Theme (palette) ----
            Color("branding.primaryColor", "Branding.PrimaryColor", "Primary colour"),
            Color("branding.secondaryColor", "Branding.SecondaryColor", "Secondary colour"),
            Color("branding.appBarColor", "Branding.AppBarColor", "App-bar colour"),
            Color("branding.backgroundColor", "Branding.BackgroundColor", "Background colour"),
            Color("branding.surfaceColor", "Branding.SurfaceColor", "Surface colour"),
            Color("branding.successColor", "Branding.SuccessColor", "Success colour"),
            Color("branding.errorColor", "Branding.ErrorColor", "Error colour"),
            Color("branding.warningColor", "Branding.WarningColor", "Warning colour"),
            Color("branding.infoColor", "Branding.InfoColor", "Info colour"),

            // ---- AI limits (on BrandingOptions + AiBuiltInOptions) ----
            Bool("branding.allowBuiltInAi", "Branding.AllowBuiltInAi", WhiteLabelCategory.Ai, "Allow built-in AI",
                "Whether the shipped built-in local LLM is offered."),
            Bool("branding.allowLocalProviders", "Branding.AllowLocalProviders", WhiteLabelCategory.Ai,
                "Allow local AI providers",
                "Whether local/self-hosted OpenAI-compatible endpoints (Ollama, LM Studio, vLLM) may be configured."),
            List("branding.allowedAiProviderKinds", "Branding.AllowedAiProviderKinds", WhiteLabelCategory.Ai,
                "Allowed AI provider kinds",
                "Comma-separated AiProviderKind names the deployment permits. Empty = all kinds allowed."),
            Bool("branding.enableEconomicCalendar", "Branding.EnableEconomicCalendar", WhiteLabelCategory.Ai,
                "Enable economic calendar",
                "Hard gate for the economic calendar feature (build-level). Off removes it entirely."),
            Bool("branding.enableCot", "Branding.EnableCot", WhiteLabelCategory.Ai,
                "Enable Commitment of Traders",
                "Hard gate for the Commitment of Traders (COT) feature (build-level). Off removes it entirely."),
            Bool("ai.builtIn.enabled", "Ai.BuiltIn.Enabled", WhiteLabelCategory.Ai, "Built-in AI seeded",
                "Whether the built-in ONNX local LLM is seeded/offered (combined with Allow built-in AI)."),
            Bool("branding.allowAiModelManagement", "Branding.AllowAiModelManagement", WhiteLabelCategory.Ai,
                "Allow AI model management",
                "Whether users can browse an endpoint's models and bind AI features to specific models."),

            // ---- Accounts ----
            List("accounts.allowedBrokers", "Accounts.AllowedBrokers", WhiteLabelCategory.Accounts, "Allowed brokers",
                "Comma-separated broker names. Empty = every broker allowed (no verification)."),
            Time("accounts.brokerProbeTimeout", "Accounts.BrokerProbeTimeout", WhiteLabelCategory.Accounts,
                "Broker-probe timeout", "How long the broker-probe container may run before verification fails."),
            Str("accounts.brokerProbeAlgoPath", "Accounts.BrokerProbeAlgoPath", WhiteLabelCategory.Accounts,
                "Broker-probe .algo path", "Path to the prebuilt broker-probe .algo used for manual-cID verification."),

            // ---- Registration ----
            Bool("registration.enabled", "Registration.Enabled", WhiteLabelCategory.Registration, "Registration enabled",
                "Master switch for self-service registration. When off the page and API return 404."),
            Enum("registration.mode", "Registration.Mode", WhiteLabelCategory.Registration, "Registration mode",
                typeof(RegistrationMode), "How a self-registered account clears before it can sign in."),
            Str("registration.defaultRole", "Registration.DefaultRole", WhiteLabelCategory.Registration, "Default role",
                "Role granted to a self-registered user. Never Owner/Admin."),
            Bool("registration.requireTermsAcceptance", "Registration.RequireTermsAcceptance",
                WhiteLabelCategory.Registration, "Require terms acceptance",
                "Require Terms/Privacy/Risk-disclosure consent before an account is created."),
            List("registration.allowedEmailDomains", "Registration.AllowedEmailDomains", WhiteLabelCategory.Registration,
                "Allowed email domains", "Comma-separated allow-list of email domains. Empty = any domain."),
            Bool("registration.blockDisposableEmail", "Registration.BlockDisposableEmail",
                WhiteLabelCategory.Registration, "Block disposable email",
                "Reject well-known disposable/throwaway email providers."),
            Time("registration.tokenLifetime", "Registration.TokenLifetime", WhiteLabelCategory.Registration,
                "Verification token lifetime", "Lifetime of an email-verification token."),
            Bool("registration.captcha.enabled", "Registration.Captcha.Enabled", WhiteLabelCategory.Registration,
                "CAPTCHA enabled", "Require a CAPTCHA on the public registration form."),
            Str("registration.captcha.verifyUrl", "Registration.Captcha.VerifyUrl", WhiteLabelCategory.Registration,
                "CAPTCHA verify URL", "Provider verify endpoint (reCAPTCHA/hCaptcha/Turnstile-compatible)."),
            Str("registration.captcha.siteKey", "Registration.Captcha.SiteKey", WhiteLabelCategory.Registration,
                "CAPTCHA site key", "Public CAPTCHA site key rendered on the form."),
            Secret("registration.captcha.secret", "Registration.Captcha.Secret", WhiteLabelCategory.Registration,
                "CAPTCHA secret", "Server-side CAPTCHA secret (encrypted; write-only)."),
            Bool("registration.api.enabled", "Registration.Api.Enabled", WhiteLabelCategory.Registration,
                "Provisioning API enabled", "Enable the server-to-server provisioning endpoint."),
            Secret("registration.api.secret", "Registration.Api.Secret", WhiteLabelCategory.Registration,
                "Provisioning secret", "Secret the caller presents in the X-Provision-Secret header (encrypted)."),
            Bool("registration.api.activateImmediately", "Registration.Api.ActivateImmediately",
                WhiteLabelCategory.Registration, "Provision active immediately",
                "Create provisioned accounts already active (the caller vouches for the user)."),
            Bool("registration.api.inviteMustChangePassword", "Registration.Api.InviteMustChangePassword",
                WhiteLabelCategory.Registration, "Provisioned must change password",
                "Force a provisioned user to change the temporary password on first sign-in."),
            // Registration attribute-collection policies
            AttrEnum("registration.attributes.fullName", "Registration.Attributes.FullName", "Full name policy"),
            AttrEnum("registration.attributes.displayName", "Registration.Attributes.DisplayName", "Display name policy"),
            AttrEnum("registration.attributes.country", "Registration.Attributes.Country", "Country policy"),
            AttrEnum("registration.attributes.phone", "Registration.Attributes.Phone", "Phone policy"),
            AttrEnum("registration.attributes.company", "Registration.Attributes.Company", "Company policy"),
            AttrEnum("registration.attributes.locale", "Registration.Attributes.Locale", "Locale policy"),
            AttrEnum("registration.attributes.marketingOptIn", "Registration.Attributes.MarketingOptIn",
                "Marketing opt-in policy"),
            AttrEnum("registration.attributes.ageConfirmation", "Registration.Attributes.AgeConfirmation",
                "Age-confirmation policy"),

            // ---- Email ----
            Str("email.host", "Email.Host", WhiteLabelCategory.Email, "SMTP host",
                "SMTP server host. Note: the sender type is chosen at startup — enabling email on a deployment that started without it needs a restart."),
            Int("email.port", "Email.Port", WhiteLabelCategory.Email, "SMTP port", "SMTP server port."),
            Bool("email.useStartTls", "Email.UseStartTls", WhiteLabelCategory.Email, "Use STARTTLS",
                "Whether to upgrade the SMTP connection with STARTTLS."),
            Str("email.username", "Email.Username", WhiteLabelCategory.Email, "SMTP username", "SMTP auth username."),
            Secret("email.password", "Email.Password", WhiteLabelCategory.Email, "SMTP password",
                "SMTP auth password (encrypted; write-only)."),
            Str("email.fromAddress", "Email.FromAddress", WhiteLabelCategory.Email, "From address",
                "Envelope/from address for outbound mail."),
            Str("email.fromName", "Email.FromName", WhiteLabelCategory.Email, "From name", "Display name for outbound mail."),

            // ---- Prop-firm ----
            Num("propFirm.drawdownWarnThresholdPercent", "PropFirm.DrawdownWarnThresholdPercent",
                WhiteLabelCategory.PropFirm, "Drawdown warning threshold %",
                "Equity-usage percentage at which a soft drawdown warning alert is raised (0 disables)."),

            // ---- Open API ----
            Str("openApi.publicBaseUrl", "OpenApi.PublicBaseUrl", WhiteLabelCategory.OpenApi, "Public base URL",
                "Canonical public URL used to compose the single Open API redirect URL behind a proxy/CDN."),
            Delegated("openApi.sharedApp", "OpenApi.SharedApp", WhiteLabelCategory.OpenApi, "Shared Open API app",
                "The white-label shared Open API application (client id/secret).", "openapi"),
            Delegated("openApi.rateLimits", "OpenApi.RateLimits", WhiteLabelCategory.OpenApi, "Open API rate limits",
                "Per-message-type outbound rate caps.", "openapi"),
        };

        // ---- Features (delegated to IFeatureGate) ----
        foreach (var flag in System.Enum.GetValues<FeatureFlag>())
            list.Add(new WhiteLabelOption
            {
                Key = "features." + char.ToLowerInvariant(flag.ToString()[0]) + flag.ToString()[1..],
                PropertyPath = "Features." + flag,
                Kind = WhiteLabelValueKind.Bool,
                Category = WhiteLabelCategory.Features,
                Label = SplitPascal(flag.ToString()),
                Description = $"Whether the {SplitPascal(flag.ToString())} feature is enabled for this deployment.",
                IsFeatureFlag = true
            });

        return list;
    }

    private static WhiteLabelOption Str(string key, string path, WhiteLabelCategory cat, string label, string desc) =>
        new() { Key = key, PropertyPath = path, Kind = WhiteLabelValueKind.String, Category = cat, Label = label, Description = desc };

    private static WhiteLabelOption Multi(string key, string path, WhiteLabelCategory cat, string label, string desc) =>
        new() { Key = key, PropertyPath = path, Kind = WhiteLabelValueKind.MultilineString, Category = cat, Label = label, Description = desc };

    private static WhiteLabelOption Bool(string key, string path, WhiteLabelCategory cat, string label, string desc) =>
        new() { Key = key, PropertyPath = path, Kind = WhiteLabelValueKind.Bool, Category = cat, Label = label, Description = desc };

    private static WhiteLabelOption Int(string key, string path, WhiteLabelCategory cat, string label, string desc) =>
        new() { Key = key, PropertyPath = path, Kind = WhiteLabelValueKind.Int, Category = cat, Label = label, Description = desc };

    private static WhiteLabelOption Num(string key, string path, WhiteLabelCategory cat, string label, string desc) =>
        new() { Key = key, PropertyPath = path, Kind = WhiteLabelValueKind.Number, Category = cat, Label = label, Description = desc };

    private static WhiteLabelOption Time(string key, string path, WhiteLabelCategory cat, string label, string desc) =>
        new() { Key = key, PropertyPath = path, Kind = WhiteLabelValueKind.TimeSpan, Category = cat, Label = label, Description = desc };

    private static WhiteLabelOption List(string key, string path, WhiteLabelCategory cat, string label, string desc) =>
        new() { Key = key, PropertyPath = path, Kind = WhiteLabelValueKind.StringList, Category = cat, Label = label, Description = desc };

    private static WhiteLabelOption Enum(string key, string path, WhiteLabelCategory cat, string label, Type enumType, string desc) =>
        new() { Key = key, PropertyPath = path, Kind = WhiteLabelValueKind.Enum, Category = cat, Label = label, EnumType = enumType, Description = desc };

    private static WhiteLabelOption Secret(string key, string path, WhiteLabelCategory cat, string label, string desc) =>
        new() { Key = key, PropertyPath = path, Kind = WhiteLabelValueKind.Secret, Category = cat, Label = label, Description = desc, IsSecret = true };

    private static WhiteLabelOption Color(string key, string path, string label) =>
        new() { Key = key, PropertyPath = path, Kind = WhiteLabelValueKind.Color, Category = WhiteLabelCategory.Theme, Label = label, Description = "Branded theme colour (hex)." };

    private static WhiteLabelOption AttrEnum(string key, string path, string label) =>
        new() { Key = key, PropertyPath = path, Kind = WhiteLabelValueKind.Enum, Category = WhiteLabelCategory.Registration, Label = label, EnumType = typeof(AttributePolicy), Description = "Whether this attribute is collected on the registration form (Off/Optional/Required)." };

    private static WhiteLabelOption Delegated(string key, string path, WhiteLabelCategory cat, string label, string desc, string section) =>
        new() { Key = key, PropertyPath = path, Kind = WhiteLabelValueKind.String, Category = cat, Label = label, Description = desc, OwnerEditable = false, DelegatedToSection = section };

    // Turns a PascalCase identifier ("PortfolioAgent") into a spaced human label ("Portfolio Agent").
    // Public so the feature-settings UI can render the same friendly base label for a FeatureFlag.
    public static string SplitPascal(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1])) builder.Append(' ');
            builder.Append(value[i]);
        }
        return builder.ToString();
    }
}
