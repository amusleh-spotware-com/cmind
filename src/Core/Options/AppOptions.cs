namespace Core.Options;

public sealed record AppOptions
{
    public const string SectionName = Constants.ConfigSections.App;

    public string OwnerEmail { get; init; } = string.Empty;
    public string OwnerPassword { get; init; } = string.Empty;
    public string? DataProtectionCertBase64 { get; init; }
    public string? DataProtectionCertPassword { get; init; }
    public string DefaultDockerImage { get; init; } = Constants.DockerImages.CtraderConsole;
    public string DefaultDockerTag { get; init; } = Constants.DockerImages.DefaultTag;
    public string BuildWorkRoot { get; init; } = Constants.FilePaths.BuildWorkRootDefault;
    public string BuildImage { get; init; } = Constants.DockerImages.CsharpBuildDefault;
    public int MaxAlgoBytes { get; init; } = 10 * 1024 * 1024;
    public int LockoutThreshold { get; init; } = 5;
    public TimeSpan NodeStatsPollInterval { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan BacktestCompletionPollInterval { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan RunCompletionPollInterval { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan InstanceReconcileInterval { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan InstanceStartupTimeout { get; init; } = TimeSpan.FromMinutes(10);
    // A backtest container that never exits (hung CLI) would otherwise be polled forever; force-stop
    // and fail it once it has run longer than this.
    public TimeSpan MaxBacktestDuration { get; init; } = TimeSpan.FromHours(6);
    public LocalNodeOptions LocalNode { get; init; } = new();
    public DiscoveryOptions Discovery { get; init; } = new();
    public AiOptions Ai { get; init; } = new();
    public AgentOptions Agent { get; init; } = new();
    public AlertOptions Alerts { get; init; } = new();
    public PropGuardOptions PropGuard { get; init; } = new();
    public OpenApiOptions OpenApi { get; init; } = new();
    public CopyOptions Copy { get; init; } = new();
    public PropFirmOptions PropFirm { get; init; } = new();
    public FeaturesOptions Features { get; init; } = new();
    public BrandingOptions Branding { get; init; } = new();
    public AccountsOptions Accounts { get; init; } = new();
    public RegistrationOptions Registration { get; init; } = new();
    public EmailOptions Email { get; init; } = new();
    public CalendarOptions Calendar { get; init; } = new();
}

public sealed record CalendarOptions
{
    /// <summary>
    /// Whether the ingestion worker (schedule-sync + release-poll + reconciliation) runs. Off by default —
    /// the domain, lazy read-through backfill and REST/MCP read side work without it; a deployment turns it
    /// on to proactively keep the calendar warm. Independent of the feature gate, which controls visibility.
    /// </summary>
    public bool IngestionEnabled { get; init; }

    public TimeSpan ScheduleSyncInterval { get; init; } = TimeSpan.FromHours(6);
    public TimeSpan ReleasePollInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan ReconcileInterval { get; init; } = TimeSpan.FromHours(1);

    /// <summary>How long a node's claim on the singleton ingestion worker stays valid without renewal (self-heal on death).</summary>
    public TimeSpan LeaseTtl { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>Stable identity of the node hosting the ingestion worker; defaults to the machine name.</summary>
    public string NodeName { get; init; } = string.Empty;

    /// <summary>Days of recent history the reconciliation pass re-syncs to catch late-published revisions.</summary>
    public int ReconcileLookbackDays { get; init; } = 7;

    /// <summary>Days ahead the worker syncs forward release schedules (e.g. central-bank meeting dates).</summary>
    public int ScheduleHorizonDays { get; init; } = 120;

    /// <summary>Base URL of the FRED API; a deployment supplies its own key via <see cref="FredApiKey"/>.</summary>
    public string FredBaseUrl { get; init; } = "https://api.stlouisfed.org/fred";

    /// <summary>FRED API key. Absent ⇒ the FRED source degrades to a typed failure; the rest of the calendar works.</summary>
    public string? FredApiKey { get; init; }

    /// <summary>
    /// Client-side request budget for the FRED source, kept under the provider's published limit (~120/min)
    /// so ingestion never trips a 429. Requests are spaced by <c>60s / this</c> through a shared gate.
    /// </summary>
    public int FredRequestsPerMinute { get; init; } = 100;

    /// <summary>Fallback cooldown applied to a source after a 429 that carries no usable <c>Retry-After</c>.</summary>
    public TimeSpan RateLimitBackoff { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Years of history the one-time proactive backfill pulls for the core high-impact series.</summary>
    public int BackfillYears { get; init; } = 10;

    /// <summary>Base URL of the BLS (US Bureau of Labor Statistics) public API v2 timeseries endpoint.</summary>
    public string BlsBaseUrl { get; init; } = "https://api.bls.gov/publicAPI/v2/timeseries/data/";

    /// <summary>Optional BLS registration key (raises the daily quota). Absent ⇒ the public low-quota tier.</summary>
    public string? BlsApiKey { get; init; }

    /// <summary>A source with no successful poll within this window is reported as stale in the health view.</summary>
    public TimeSpan SourceStaleAfter { get; init; } = TimeSpan.FromHours(2);

    /// <summary>Consecutive source failures that trip the reported circuit-open flag for that source.</summary>
    public int CircuitFailureThreshold { get; init; } = 5;

    /// <summary>
    /// When a blackout answer is uncertain (source/DB fault, data gap), the conservative default: <c>true</c>
    /// = fail-closed ("assume in-blackout") for risk-off bots, <c>false</c> = fail-open. Never silently
    /// green-lights trading through a data gap.
    /// </summary>
    public bool BlackoutFailClosed { get; init; } = true;

    /// <summary>Short JWT lifetime for issued Calendar API client tokens.</summary>
    public TimeSpan ApiTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Whether the outbound webhook delivery worker runs (POSTs matching released events to registered
    /// webhooks). Off by default — it makes external calls; a deployment opts in.
    /// </summary>
    public bool WebhooksEnabled { get; init; }

    /// <summary>How often the webhook worker polls for newly-released events to deliver.</summary>
    public TimeSpan WebhookPollInterval { get; init; } = TimeSpan.FromSeconds(30);
}

public sealed record CopyOptions
{
    public bool Enabled { get; init; }
    public TimeSpan ReconcileInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a node's claim on a running copy profile stays valid without renewal. A node renews the
    /// lease every reconcile; if a node dies, its profiles become reclaimable by any other node once the
    /// lease expires, so copying self-heals across a horizontally scaled cluster. Keep it a few reconcile
    /// intervals so a slow cycle does not cause a spurious hand-off.
    /// </summary>
    public TimeSpan LeaseTtl { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Stable identity of the node hosting copy engines. Each running profile is claimed by exactly
    /// one node so co-located supervisors (Web local node + CopyAgent worker) never double-execute.
    /// Defaults to the machine name; set distinctly when two supervisors share a host.
    /// </summary>
    public string NodeName { get; init; } = string.Empty;

    /// <summary>
    /// S4 bounded claim: the maximum number of running copy profiles a single node will host. When &gt; 0 a
    /// node claims at most this many (atomic, skip-locked), so profiles spread across replicas instead of the
    /// first supervisor grabbing them all. 0 = unbounded (a single node hosts everything — the default).
    /// </summary>
    public int MaxProfilesPerNode { get; init; }

    /// <summary>
    /// Execution transparency (Phase 3): when true the copy host emits a per-copy execution fact
    /// (latency, slippage, fill vs failure) that a background drainer persists to the CopyExecution log,
    /// powering the transparency read model. Off by default — the host emits to a no-op sink, so the
    /// trading hot path and every test are unaffected.
    /// </summary>
    public bool TransparencyEnabled { get; init; }

    /// <summary>
    /// Operational notifications (2b): when true the copy host emits a safety notification (destination
    /// tripped, account-protection/prop-rule breach, flatten-all) that a background drainer persists to the
    /// per-owner CopyNotification feed. On by default — these are user-facing safety alerts; set false to
    /// silence the feed. When false the host emits to a no-op sink (unchanged engine).
    /// </summary>
    public bool NotificationsEnabled { get; init; } = true;

    /// <summary>
    /// Money-manager performance fees (Phase 4): when true a background service periodically settles each
    /// fee-configured destination's high-water-mark performance fee against its live equity and records a
    /// CopyFeeAccrual. Off by default — the money-manager layer is opt-in.
    /// </summary>
    public bool FeesEnabled { get; init; }

    /// <summary>How often the fee settlement service polls equity and settles high-water-mark fees.</summary>
    public TimeSpan FeeSettlementInterval { get; init; } = TimeSpan.FromHours(1);
}

public sealed record PropFirmOptions
{
    /// <summary>
    /// When true, nodes host live prop-firm challenge trackers: each active challenge is claimed on a
    /// self-healing lease and its account is tracked over the cTrader Open API. Off by default — tracking
    /// requires a real authorised trading account. The domain and manual-equity path run without it.
    /// </summary>
    public bool Enabled { get; init; }

    public TimeSpan ReconcileInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How often a running tracker recomputes equity and feeds it to the challenge aggregate.</summary>
    public TimeSpan EquityPollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a node's claim on an active challenge stays valid without renewal (self-heal on node death).</summary>
    public TimeSpan LeaseTtl { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>Equity-usage percentage at which a soft drawdown warning alert is raised (0 disables it).</summary>
    public double DrawdownWarnThresholdPercent { get; init; } = 80;

    /// <summary>Stable identity of the node hosting trackers; defaults to the machine name.</summary>
    public string NodeName { get; init; } = string.Empty;
}

public sealed record OpenApiOptions
{
    public bool Enabled { get; init; }

    /// <summary>
    /// Canonical public URL of this deployment (e.g. <c>https://cmind.mybroker.com</c>), used to compose the
    /// single Open API redirect URL <c>{PublicBaseUrl}{CallbackPath}</c> deterministically behind a
    /// proxy/CDN. When absent the redirect URL falls back to the inbound request host.
    /// </summary>
    public string? PublicBaseUrl { get; init; }

    /// <summary>
    /// White-label shared Open API application. When enabled with credentials, one app is seeded and every
    /// user authorizes accounts through it — the per-user app option disappears.
    /// </summary>
    public SharedOpenApiAppOptions SharedApp { get; init; } = new();

    /// <summary>
    /// Per-message-type outbound rate caps (messages/sec) keyed by <c>OpenApiRateCategory</c> name
    /// (<c>General</c>, <c>HistoricalData</c>). Empty ⇒ safe built-in defaults. A category set to <c>0</c>
    /// is unlimited. An owner may override these at runtime from settings.
    /// </summary>
    public IReadOnlyDictionary<string, int> RateLimits { get; init; } = new Dictionary<string, int>();
    public string AuthBaseUrl { get; init; } = Constants.OpenApiEndpoints.AuthBaseUrl;
    public string LiveHost { get; init; } = Constants.OpenApiEndpoints.LiveHost;
    public string DemoHost { get; init; } = Constants.OpenApiEndpoints.DemoHost;
    public int Port { get; init; } = Constants.OpenApiEndpoints.Port;
    public TimeSpan TokenRefreshThreshold { get; init; } = TimeSpan.FromDays(3);
    public TimeSpan TokenRefreshInterval { get; init; } = TimeSpan.FromHours(1);
    // When a refresh keeps failing and the token is within this window of expiry, escalate once with a
    // critical alert so the owner can re-authorize before copy/prop-firm operations lose their token.
    public TimeSpan TokenRefreshCriticalWindow { get; init; } = TimeSpan.FromHours(6);
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan InboundWatchdogTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan BackoffInitial { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan BackoffMax { get; init; } = TimeSpan.FromSeconds(60);
}

public sealed record SharedOpenApiAppOptions
{
    public bool Enabled { get; init; }
    public string Name { get; init; } = "Shared Open API Application";
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
}

public sealed record PropGuardOptions
{
    public bool Enabled { get; init; }
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(1);
}

public sealed record AlertOptions
{
    public bool Enabled { get; init; }
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(5);
    public int MaxRulesPerCycle { get; init; } = 10;
}

public sealed record AgentOptions
{
    public bool Enabled { get; init; }
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(30);
    public int MaxProposalsPerCycle { get; init; } = Constants.AgentConstants.MaxProposalsPerCycleDefault;
}

public sealed record AiOptions
{
    // Legacy single-key config (Anthropic). Still honoured: when no provider credential rows exist it is
    // imported as the default active Anthropic provider, so existing deployments keep working untouched.
    public string? ApiKey { get; init; }
    public string Model { get; init; } = Constants.AiConstants.DefaultModel;
    public string BaseUrl { get; init; } = Constants.AiConstants.DefaultBaseUrl;
    public int MaxTokens { get; init; } = Constants.AiConstants.DefaultMaxTokens;
    public bool RiskGuardEnabled { get; init; }
    public bool RiskGuardAutoStop { get; init; }
    public TimeSpan RiskGuardInterval { get; init; } = TimeSpan.FromMinutes(5);
    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);

    // Deployment-seeded providers imported into the store on startup if absent (idempotent), so an ops
    // team can ship a local-LLM or cloud deployment purely via appsettings/env, no UI needed.
    public Ai.AiProviderKind? ActiveProvider { get; init; }
    public IReadOnlyList<AiProviderOptions> Providers { get; init; } = [];

    // Built-in real local LLM (Microsoft.ML.OnnxRuntimeGenAI), shipped and enabled by default so every
    // deployment has working AI with no key. Seeded + activated on first startup when allowed and no
    // provider exists yet.
    public AiBuiltInOptions BuiltIn { get; init; } = new();
}

public sealed record AiBuiltInOptions
{
    /// <summary>Whether the built-in ONNX local LLM is offered/seeded. Combined with the white-label
    /// <c>App:Branding:AllowBuiltInAi</c> gate.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Directory of the ONNX GenAI model (config + weights). Relative paths resolve under the app
    /// base directory. When absent, the provider degrades to a typed failure with an install hint.</summary>
    public string ModelPath { get; init; } = Constants.AiConstants.BuiltInModelDefaultPath;

    public int MaxTokens { get; init; } = 1024;
}

public sealed record AiProviderOptions
{
    public Ai.AiProviderKind Kind { get; init; }
    public string? BaseUrl { get; init; }
    public string? Model { get; init; }
    public string? ApiKey { get; init; }
    public int? MaxTokens { get; init; }
    public AiCapabilityOptions? Capabilities { get; init; }
}

public sealed record AiCapabilityOptions
{
    public bool? SupportsWebSearch { get; init; }
    public bool? SupportsVision { get; init; }
    public bool? SupportsSystemRole { get; init; }
    public bool? SupportsTools { get; init; }
}

public sealed record DiscoveryOptions
{
    public bool Enabled { get; init; }
    public string? JoinToken { get; init; }
    public TimeSpan HeartbeatTtl { get; init; } = TimeSpan.FromSeconds(90);
    public TimeSpan MonitorInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    // Extra margin beyond HeartbeatTtl before a non-terminal instance on an unreachable node is
    // reclaimed (failed). Guards against reclaiming during a brief heartbeat gap that recovers.
    public TimeSpan InstanceReclaimGrace { get; init; } = TimeSpan.FromSeconds(60);
}

public sealed record LocalNodeOptions
{
    public bool Enabled { get; init; }
    public string Name { get; init; } = Constants.LocalNodeDefaults.Name;
    public string WorkRoot { get; init; } = Constants.FilePaths.LocalRunWorkRootDefault;
    public int MaxInstances { get; init; } = Constants.LocalNodeDefaults.MaxInstances;
}
