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

    /// <summary>
    /// Whether the dashboard shows the "Powered by cMind" link back to the project site. Defaults to
    /// <c>true</c>; a white-label deployment sets it to <c>false</c> to hide the credit entirely.
    /// </summary>
    public bool ShowSiteLink { get; init; } = BrandingDefaults.ShowSiteLink;

    /// <summary>
    /// When <c>true</c>, every user must set up two-factor authentication before they can use the app:
    /// after the password step a user without MFA is forced through enrollment on first login. Defaults to
    /// <c>false</c> — 2FA stays opt-in from the profile page. A regulated white-label deployment sets it to
    /// <c>true</c> to make an authenticator app mandatory for all accounts.
    /// </summary>
    public bool RequireMfa { get; init; }

    /// <summary>
    /// Whether the shipped built-in local LLM (Microsoft.ML.OnnxRuntimeGenAI) is offered. Defaults to
    /// <c>true</c> — every user gets working AI with no key. A white-label deployment sets it <c>false</c>
    /// to remove the built-in model entirely (e.g. to force its own provider).
    /// </summary>
    public bool AllowBuiltInAi { get; init; } = true;

    /// <summary>
    /// Whether local / self-hosted providers (loopback or private-network OpenAI-compatible endpoints,
    /// e.g. Ollama/LM Studio/vLLM) may be configured. Defaults to <c>true</c>. A white-label deployment
    /// that must keep AI on approved clouds sets it <c>false</c>.
    /// </summary>
    public bool AllowLocalProviders { get; init; } = true;

    /// <summary>
    /// The exact set of provider kinds a deployment permits (names of <c>AiProviderKind</c>). Empty = all
    /// kinds allowed. A white-label deployment lists only the kinds it sanctions (e.g. just
    /// <c>Anthropic</c>, <c>OpenAiCompatible</c>) to lock down which AI providers users may add.
    /// </summary>
    public IReadOnlyList<string> AllowedAiProviderKinds { get; init; } = [];
}
