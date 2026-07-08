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
    public AiOptions Ai { get; init; } = new();
    public AgentOptions Agent { get; init; } = new();
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

public sealed record LocalNodeOptions
{
    public bool Enabled { get; init; }
    public string Name { get; init; } = Constants.LocalNodeDefaults.Name;
    public string WorkRoot { get; init; } = Constants.FilePaths.LocalRunWorkRootDefault;
    public int MaxInstances { get; init; } = Constants.LocalNodeDefaults.MaxInstances;
}
