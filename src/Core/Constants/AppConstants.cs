namespace Core.Constants;

public static class AuthPolicies
{
    public const string Owner = nameof(Owner);
    public const string AdminOrAbove = nameof(AdminOrAbove);
    public const string UserOrAbove = nameof(UserOrAbove);
}

public static class RateLimitPolicies
{
    public const string Auth = nameof(Auth);
    public const int AuthPermitPerWindow = 20;
    public const int AuthWindowSeconds = 60;
}

public static class AuthLockout
{
    public const int MaxFailedAttempts = 5;
    public const int LockoutMinutes = 15;
}

public static class AuthSchemes
{
    public const string McpKey = nameof(McpKey);
    public const string Bearer = "Bearer";
    public const string McpTokenPrefix = "mcpk_";
}

public static class EncryptionPurposes
{
    public const string CtidPassword = "ctid.password";
    public const string CbotAlgo = "cbot.algo";
    public const string CbotSource = "cbot.source";
    public const string NodeApiSecret = "node.api.secret";
}

public static class NodeAgentAuth
{
    public const string Issuer = "app-main";
    public const string Audience = "app-node";
    public const int TokenLifetimeSeconds = 300;
    public const int MinSecretLength = 32;
}

public static class NodeAgentProtocol
{
    // Wire-contract version for the main node <-> ExternalNode agent HTTP API. Independent of
    // the product SemVer: bump ONLY on a breaking change to the agent contract (routes, request/
    // response shapes, semantics). The main node stamps every request with HeaderName; the agent
    // rejects mismatched versions with 426 Upgrade Required.
    public const string HeaderName = "X-Node-Protocol-Version";
    public const int Version = 1;
}

public static class NodeAgentRoutes
{
    public const string Base = "/api";
    public const string Info = "/api/info";
    // Start is keyed by the instance id (used for the work dir + container label); all other
    // operations are keyed by the returned container id, which is stable across the instance's
    // TPH state transitions (starting -> running -> ...).
    public const string Start = "/api/containers";
    public const string NodeStats = "/api/node/stats";
    public const string NodeClean = "/api/node/clean";

    public static string Status(string containerId) => $"/api/containers/{containerId}/status";
    public static string Report(string containerId) => $"/api/containers/{containerId}/report";
    public static string Stop(string containerId) => $"/api/containers/{containerId}/stop";
    public static string Logs(string containerId) => $"/api/containers/{containerId}/logs";
}

public static class ConfigSections
{
    public const string App = "App";
}

public static class AiConstants
{
    public const string DefaultModel = "claude-opus-4-8";
    public const string DefaultBaseUrl = "https://api.anthropic.com/";
    public const string MessagesPath = "v1/messages";
    public const string AnthropicVersion = "2023-06-01";
    public const string AnthropicVersionHeader = "anthropic-version";
    public const string ApiKeyHeader = "x-api-key";
    public const int DefaultMaxTokens = 8000;
    public const string WebSearchToolType = "web_search_20260209";
    public const string WebSearchToolName = "web_search";
    public const string DisabledMessage = "AI features are not configured (set App:Ai:ApiKey).";
    public const int TuneAdviceMaxTokens = 1500;
    public const int DigestMaxTokens = 2000;
    public const int DigestMaxInstances = 60;
    public const int DebateMaxTokens = 2000;
    public const int ExposureMaxTokens = 2000;
    public const int ExposureMaxInstances = 40;
}

public static class RiskGuardConstants
{
    public const int ActionMaxTokens = 1000;
    public const string ActionStop = "stop";
    public const string SeverityCritical = "critical";
    public const string AuditAction = "AiRiskGuardStop";
    public const string AuditEntityType = "Instance";
    public const int MaxReasonChars = 512;
}

public static class AgentConstants
{
    public const string ProposalKindBacktest = "Backtest";
    public const int ActionMaxTokens = 1500;
    public const int MaxReasoningChars = 4000;
    public const int MaxProposalsPerCycleDefault = 3;
    public const string DefaultSymbol = "EURUSD";
    public const string DefaultTimeframe = "h1";
}

public static class AlertConstants
{
    public const int AssessMaxTokens = 1200;
    public const int MaxMessageChars = 1000;
    public const int MinIntervalMinutes = 5;
    public const int MaxIntervalMinutes = 1440;
    public const int DefaultIntervalMinutes = 60;
    public const string SeverityInfo = "info";
    public const string SeverityWarning = "warning";
    public const string SeverityCritical = "critical";
}

public static class ConnectionStrings
{
    public const string AppDb = "appdb";
}

public static class HubRoutes
{
    public const string Logs = "/hubs/logs";
}

public static class HealthEndpoints
{
    public const string Health = "/health";
    public const string Alive = "/alive";
    public const string LiveTag = "live";
    public const string Version = "/version";
}

public static class DockerLabels
{
    public const string User = "app.user";
    public const string Instance = "app.instance";
    public const string Type = "app.type";
}

public static class FilePaths
{
    public const string ContainerWorkMount = "/mnt/work";
    public const string ContainerDataDir = "/mnt/work/data";
    public const string CbotAlgoFile = "cbot.algo";
    public const string ParamsCbotsetFile = "params.cbotset";
    public const string CtidPwdFile = "ctid.pwd";
    public const string ReportJsonFile = "report.json";
    public const string ReportHtmlFile = "report.html";
    public static readonly string BuildWorkRootDefault =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "app", "builds");
    public static readonly string LocalRunWorkRootDefault =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "app", "local-runs");
    public const string DataRootPrefix = "/var/app/";
}

public static class LocalNodeDefaults
{
    public const string Name = "local";
    public const int MaxInstances = 5;
}

public static class DockerImages
{
    public const string CtraderConsole = "ghcr.io/spotware/ctrader-console";
    public const string CsharpBuildDefault = "mcr.microsoft.com/dotnet/sdk:8.0";
    public const string DefaultTag = "latest";
}

public static class CliFlags
{
    public const string Ctid = "--ctid";
    public const string PwdFile = "--pwd-file";
    public const string Account = "--account";
    public const string Symbol = "--symbol";
    public const string Period = "--period";
    public const string DataDir = "--data-dir";
    public const string Start = "--start";
    public const string End = "--end";
    public const string DataMode = "--data-mode";
    public const string ReportJson = "--report-json";
    public const string Report = "--report";
    public const string ExitOnStop = "--exit-on-stop";
}

public static class BacktestDefaults
{
    public const string DataMode = "m1";
    public const string DateFormat = "dd/MM/yyyy HH:mm";
}

public static class CliCommands
{
    public const string Run = "run";
    public const string Backtest = "backtest";
}

public static class DockerCommands
{
    public const string RunDetached = "run -d";
    public const string Stop = "docker stop";
    public const string RemoveForce = "docker rm -f";
    public const string LogsFollow = "docker logs -f";
    public const string StatsNoStream = "docker stats --no-stream --format '{{.CPUPerc}}|{{.MemUsage}}'";
    public const string RunBuild = "run --rm --memory=2g --cpus=2";
    public const string NameFlag = "--name";
    public const string LabelFlag = "--label";
    public const string VolumeFlag = "-v";
    public const string ContainerNamePrefix = "app-";
    public const string BuildCommand = "sh -c \"cd /work && dotnet build -c Release -o /work/out 2>&1\"";
    public const string BuildOutDir = "out";
    public const string BuildMount = "/work";
    public const string BuildNugetVolume = "app-nuget-cache";
    public const string BuildNugetMount = "/root/.nuget/packages";
    public const string AlgoExtensionPattern = "*.algo";
}
