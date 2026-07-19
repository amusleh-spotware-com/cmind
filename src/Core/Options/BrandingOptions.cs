using Core.Constants;
using Core.Nodes;

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

    /// <summary>
    /// Whether the AI model-management surface is offered — browsing the models a provider advertises and
    /// binding each AI feature to a specific model. Defaults to <c>true</c>. A white-label deployment (or the
    /// owner) sets it <c>false</c> to keep AI on a single fixed active model per scope and hide the
    /// browse/binding UI and endpoints.
    /// </summary>
    public bool AllowAiModelManagement { get; init; } = true;

    /// <summary>
    /// White-label hard gate for the economic calendar (Tier 2). Defaults to <c>true</c> — the calendar,
    /// its REST API and MCP tools ship on. A reseller sets it <c>false</c> to remove the feature entirely
    /// from that build: it never appears and an operator cannot re-enable it via the runtime toggle.
    /// </summary>
    public bool EnableEconomicCalendar { get; init; } = true;

    /// <summary>
    /// White-label hard gate for the Commitment of Traders (COT) report (Tier 2). Defaults to <c>true</c> —
    /// the COT browser, its REST API and MCP tools ship on. A reseller sets it <c>false</c> to remove the
    /// feature entirely from that build: it never appears and an operator cannot re-enable it at runtime.
    /// </summary>
    public bool EnableCot { get; init; } = true;

    /// <summary>
    /// How much of the Nodes surface this deployment exposes. Defaults to <see cref="NodesUiMode.Full"/>
    /// (list plus manual add/delete). A white-label deployment sets <see cref="NodesUiMode.Monitor"/> to
    /// keep a read-only view but drop manual add/delete, or <see cref="NodesUiMode.Hidden"/> to remove the
    /// nav link, page and manual API entirely and run the cluster purely through node auto-discovery.
    /// </summary>
    public NodesUiMode NodesUi { get; init; } = NodesUiMode.Full;

    /// <summary>
    /// When <c>true</c>, only the owner may see and manage nodes; otherwise the whole admin-or-above staff
    /// surface can. Defaults to <c>false</c>. Normal users never see nodes regardless of this flag.
    /// </summary>
    public bool RestrictNodesToOwner { get; init; }

    /// <summary>
    /// Default display time zone (canonical IANA id, e.g. <c>Europe/London</c>) shown to a user who has not
    /// chosen their own and whose browser zone was not detected. Defaults to <c>UTC</c>. An owner can retune
    /// it at runtime from Deployment settings; every displayed time is rendered in the effective zone.
    /// </summary>
    public string DefaultTimeZone { get; init; } = SupportedTimeZones.Default;
}
