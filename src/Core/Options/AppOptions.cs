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
    public string AuthBaseUrl { get; init; } = Constants.OpenApiEndpoints.AuthBaseUrl;
    public string LiveHost { get; init; } = Constants.OpenApiEndpoints.LiveHost;
    public string DemoHost { get; init; } = Constants.OpenApiEndpoints.DemoHost;
    public int Port { get; init; } = Constants.OpenApiEndpoints.Port;
    public TimeSpan TokenRefreshThreshold { get; init; } = TimeSpan.FromDays(3);
    public TimeSpan TokenRefreshInterval { get; init; } = TimeSpan.FromHours(1);
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan InboundWatchdogTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan BackoffInitial { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan BackoffMax { get; init; } = TimeSpan.FromSeconds(60);
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
    public string? ApiKey { get; init; }
    public string Model { get; init; } = Constants.AiConstants.DefaultModel;
    public string BaseUrl { get; init; } = Constants.AiConstants.DefaultBaseUrl;
    public int MaxTokens { get; init; } = Constants.AiConstants.DefaultMaxTokens;
    public bool RiskGuardEnabled { get; init; }
    public bool RiskGuardAutoStop { get; init; }
    public TimeSpan RiskGuardInterval { get; init; } = TimeSpan.FromMinutes(5);
    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}

public sealed record DiscoveryOptions
{
    public bool Enabled { get; init; }
    public string? JoinToken { get; init; }
    public TimeSpan HeartbeatTtl { get; init; } = TimeSpan.FromSeconds(90);
    public TimeSpan MonitorInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);
}

public sealed record LocalNodeOptions
{
    public bool Enabled { get; init; }
    public string Name { get; init; } = Constants.LocalNodeDefaults.Name;
    public string WorkRoot { get; init; } = Constants.FilePaths.LocalRunWorkRootDefault;
    public int MaxInstances { get; init; } = Constants.LocalNodeDefaults.MaxInstances;
}
