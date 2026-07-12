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

    // Self-service registration is public and anonymous, so it is throttled harder than auth: a handful of
    // sign-up / verify / resend attempts per IP per minute is plenty for a human and starves scripted abuse.
    public const string Registration = nameof(Registration);
    public const int RegistrationPermitPerWindow = 5;
    public const int RegistrationWindowSeconds = 60;
}

public static class RegistrationConstants
{
    // Bytes of entropy in an email-verification token; the raw token is url-safe base64 of these bytes and
    // only its SHA-256 hash is stored. 32 bytes = 256 bits.
    public const int VerificationTokenBytes = 32;

    // Header the provisioning caller presents its shared secret in.
    public const string ProvisionSecretHeader = "X-Provision-Secret";

    // Max length accepted for any free-text profile attribute (name, company, ...).
    public const int MaxProfileTextLength = 128;

    public const int RoleRankUser = 2;
    public const int RoleRankViewer = 3;
}

public static class AuthLockout
{
    public const int MaxFailedAttempts = 5;
    public const int LockoutMinutes = 15;
}

public static class DatabaseDefaults
{
    public const int MaxRetryCount = 6;
    public const int MaxRetryDelaySeconds = 30;
    public const int CommandTimeoutSeconds = 30;

    // Postgres advisory-lock key serializing schema migration + owner seeding across web/mcp replicas
    // so a rolling deploy or scale-out never runs migrations concurrently. Stable, app-wide constant.
    public const long MigrationAdvisoryLockKey = 902026_0711L;
}

public static class NodeAgentHttp
{
    // Idempotent reads (status/report/stats) are retried on transient node/network failures.
    public const int ReadRetryCount = 3;
    public const int ReadRetryBaseDelayMilliseconds = 200;
    public const int ReadTotalTimeoutSeconds = 30;
    public const int ReadAttemptTimeoutSeconds = 10;
    // Non-idempotent writes (start/stop/clean) get a timeout but are never retried — a retried start
    // could double-launch a container.
    public const int WriteTimeoutSeconds = 30;
}

public static class AiHttp
{
    // AI completions are long-running (web search, vision, self-repair loops) so the timeouts are
    // generous. Transient 5xx / network failures are retried; the client always degrades to a typed
    // AiResult.Fail rather than throwing into a page, tool, or hosted service.
    public const int RetryCount = 2;
    public const int RetryBaseDelaySeconds = 1;
    public const int AttemptTimeoutSeconds = 120;
    public const int TotalTimeoutSeconds = 300;
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
    public const string OpenApiClientSecret = "openapi.client_secret";
    public const string OpenApiAccessToken = "openapi.access_token";
    public const string OpenApiRefreshToken = "openapi.refresh_token";
    public const string OpenApiOAuthState = "openapi.oauth_state";
    public const string AiApiKey = "ai.api_key";
    public const string MfaSecret = "mfa.secret";
    public const string MfaPendingCookie = "mfa.pending_cookie";
}

public static class MfaConstants
{
    // RFC 6238 authenticator defaults (Google/Microsoft/Authy compatible): 6 digits, 30s step, SHA1.
    public const int Digits = 6;
    public const int PeriodSeconds = 30;
    // Accepted drift on verification: +/- this many 30s windows to tolerate clock skew.
    public const int VerificationWindowSteps = 1;
    public const int SecretSizeBytes = 20;

    // Single-use recovery codes issued at enrollment.
    public const int BackupCodeCount = 10;
    // Characters per code (Crockford-ish base32, unambiguous). Shown grouped as XXXXX-XXXXX.
    public const int BackupCodeLength = 10;

    // Name of the short-lived cookie holding the half-authenticated state between the password step
    // and the TOTP challenge. Never an auth cookie; deleted the moment the challenge succeeds/fails.
    public const string PendingCookieName = "app_mfa_pending";
    public const int PendingChallengeLifetimeMinutes = 5;

    // Claim stamped on a fully-authenticated principal that still owes mandatory enrollment
    // (white-label App:Branding:RequireMfa). The enforcement guard redirects until MFA is enabled.
    public const string SetupRequiredClaimType = "app:mfa_setup_required";
    public const string SetupRequiredClaimValue = "true";
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
    // Wire-contract version for the main node <-> CtraderCliNode agent HTTP API. Independent of
    // the product SemVer: bump ONLY on a breaking change to the agent contract (routes, request/
    // response shapes, semantics). The main node stamps every request with HeaderName; the agent
    // rejects mismatched versions with 426 Upgrade Required.
    public const string HeaderName = "X-Node-Protocol-Version";
    public const int Version = 1;
}

public static class NodeDiscoveryRoutes
{
    // Route on the MAIN node that agents self-register + heartbeat against.
    public const string Register = "/api/nodes/register";
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

public static class BrandingDefaults
{
    public const string ProductName = "cMind";
    public const string Description =
        "cMind — build, run, and backtest cTrader cBots across remote nodes and the local host.";
    public const string FaviconUrl = "favicon.svg";
    public const string PrimaryColor = "#26C281";
    public const string SecondaryColor = "#1FB97A";
    public const string AppBarColor = "#141414";
    public const string BackgroundColor = "#1A1A1A";
    public const string SurfaceColor = "#262626";
    public const string SuccessColor = "#26C281";
    public const string ErrorColor = "#E74C3C";
    public const string WarningColor = "#F39C12";
    public const string InfoColor = "#3498DB";

    // The project documentation site, surfaced as the dashboard "Powered by cMind" link.
    public const string SiteUrl = "https://amusleh-spotware-com.github.io/cmind";
    public const bool ShowSiteLink = true;
}

public static class FeatureSettings
{
    // AppSetting key namespace for owner-set runtime feature overrides. A key of the form
    // "feature.<FeatureFlag>" stores "true"/"false"; absence means "use the config baseline".
    public const string OverrideKeyPrefix = "feature.";
    public const string OverrideCacheKey = "features.overrides";
    public static readonly TimeSpan OverrideCacheTtl = TimeSpan.FromSeconds(10);

    public static string OverrideKey(Features.FeatureFlag flag) => OverrideKeyPrefix + flag;
}

public static class OpenApiEndpoints
{
    public const string AuthBaseUrl = "https://openapi.ctrader.com/";
    public const string AuthorizePath = "apps/auth";
    public const string TokenPath = "apps/token";
    public const string LiveHost = "live.ctraderapi.com";
    public const string DemoHost = "demo.ctraderapi.com";
    public const int Port = 5035;
    public const string StateCookieName = "oapi_oauth_state";
    public const string CallbackPath = "/openapi/callback";
    public const string AuthorizedRedirectPath = "/accounts";
    public const string InviteRedirectPath = "/";
    public const int SuccessRedirectDelaySeconds = 5;
}

public static class ObservabilityDefaults
{
    public const string WebServiceName = "cmind-web";
    public const string McpServiceName = "cmind-mcp";
    public const string NodeAgentServiceName = "cmind-node-agent";
    public const string CopyAgentServiceName = "cmind-copy-agent";
    public const string ServiceNamespace = "cmind";
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string AzureMonitorConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";
    public const string ServiceNameProperty = "service.name";
    public const string ServiceVersionProperty = "service.version";
    public const string ServiceNamespaceProperty = "service.namespace";
    public const string DeploymentEnvironmentProperty = "deployment.environment";
    public const string TraceIdProperty = "trace_id";
    public const string SpanIdProperty = "span_id";
    public const string CopyMeterName = "cMind.Copy";
}

public static class CopyDefaults
{
    // G8 rejection circuit breaker: after this many consecutive open failures a destination is "tripped"
    // and receives no new opens until the cooldown elapses (existing positions are still managed/closed).
    public const int RejectionBudget = 5;
    public static readonly TimeSpan CircuitCooldown = TimeSpan.FromSeconds(60);
    // Account-protection equity-guard poll interval: how often each destination's live equity is checked
    // against its protection policy.
    public static readonly TimeSpan EquityGuardInterval = TimeSpan.FromSeconds(15);
    // C13 slave-pending fill-correlation: how often to check mirrored pendings, and how long a mirrored
    // pending may rest after the master's has vanished before the slave copy is cancelled.
    public static readonly TimeSpan PendingCheckInterval = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan PendingCorrelationTimeout = TimeSpan.FromSeconds(120);
    // G4 bounded-concurrency dispatch: how many destinations a single source event fans out to at once, so
    // the Nth slave doesn't queue behind the first N-1. Per-destination host state is thread-safe
    // (ConcurrentDictionary) and network I/O is isolated per destination, so this is a pure latency win.
    public const int MaxDestinationConcurrency = 4;
    // Phase 3 execution transparency: bounded buffer between the copy host (producer, hot path) and the DB
    // drainer (consumer). Full = drop the oldest fact rather than block trading. Drainer flushes a batch on
    // this interval.
    public const int CopyExecutionChannelCapacity = 10_000;
    // Operational notifications are low-volume (breach events only) but user-facing, so a smaller buffer.
    public const int CopyNotificationChannelCapacity = 2_000;
    public const int CopyExecutionDrainBatchSize = 500;
    public static readonly TimeSpan CopyExecutionDrainInterval = TimeSpan.FromSeconds(5);
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
    public const string DisabledMessage = "AI features are not configured — add an API key in Settings → AI.";

    // AppSetting key holding the owner-set Anthropic API key, encrypted via ISecretProtector
    // (EncryptionPurposes.AiApiKey) and base64-encoded. Overrides App:Ai:ApiKey config when present.
    public const string ApiKeySettingKey = "ai.api_key";
    public const string ApiKeyCacheKey = "ai.api_key.value";
    public static readonly TimeSpan ApiKeyCacheTtl = TimeSpan.FromSeconds(30);
    public const int TuneAdviceMaxTokens = 1500;
    public const int DigestMaxTokens = 2000;
    public const int DigestMaxInstances = 60;
    public const int DebateMaxTokens = 2000;
    public const int ExposureMaxTokens = 2000;
    public const int ExposureMaxInstances = 40;

    // Named HttpClient shared by every provider adapter — one resilience pipeline (timeouts + retry)
    // covers cloud and local endpoints identically. Base URL/headers are set per request.
    public const string HttpClientName = "ai";

    // Cache of the resolved active provider (or null = "no active provider"), refreshed on any store
    // mutation. Mirrors the old key cache TTL so gating stays cheap on the request path.
    public const string ActiveProviderCacheKey = "ai.active_provider";
    public static readonly TimeSpan ActiveProviderCacheTtl = TimeSpan.FromSeconds(30);

    // Built-in demo provider placeholders — the endpoint/model are unused (the demo runs in-process) but
    // the credential still stores a valid base URL + model.
    public const string DemoBaseUrl = "https://demo.local/";
    public const string DemoModel = "cmind-demo";

    // Built-in ONNX local LLM placeholders (endpoint unused — it runs in-process) + default model dir.
    public const string BuiltInBaseUrl = "https://builtin.local/";
    public const string BuiltInModel = "built-in-onnx";
    public const string BuiltInModelDefaultPath = "models/onnx";
    public const string BuiltInUnavailableMessage =
        "The built-in local AI model is not installed. Add an ONNX GenAI model under the configured path " +
        "(App:Ai:BuiltIn:ModelPath) or configure another provider in Settings → AI.";

    // Vision requested against a provider that cannot accept images (e.g. a text-only local model).
    public const string VisionUnsupportedMessage =
        "The active AI provider does not support image input. Switch to a vision-capable provider in Settings → AI.";
}

// Wire constants for the OpenAI Chat Completions family — the dominant format also spoken by Azure
// OpenAI, Mistral, Groq, Together, OpenRouter, DeepSeek and every local runtime (Ollama, LM Studio,
// vLLM, llama.cpp, LocalAI). One adapter covers them all; only the base URL + model + key differ.
public static class OpenAiConstants
{
    public const string ChatCompletionsPath = "chat/completions";
    public const string AuthorizationHeader = "Authorization";
    public const string BearerPrefix = "Bearer ";
    public const string DefaultBaseUrl = "https://api.openai.com/v1/";
    public const string OllamaHintBaseUrl = "http://localhost:11434/v1/";
    public const string WebSearchToolType = "web_search_preview";
}

public static class AzureOpenAiConstants
{
    public const string ApiKeyHeader = "api-key";
    public const string ApiVersion = "2024-10-21";
    public const string ApiVersionQuery = "api-version";
    // Deployment path: openai/deployments/{model}/chat/completions?api-version=...
    public static string ChatCompletionsPath(string deployment) => $"openai/deployments/{deployment}/chat/completions";
}

public static class GeminiConstants
{
    public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/";
    public const string KeyQuery = "key";
    public const string GoogleSearchTool = "google_search";
    // generateContent path: v1beta/models/{model}:generateContent
    public static string GenerateContentPath(string model) => $"v1beta/models/{model}:generateContent";
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

public static class PropGuardConstants
{
    public const string AuditAction = "PropGuardFlatten";
    public const string AuditEntityType = "TradingAccount";
    public const int MaxConcurrentCap = 100;
    public const int MaxRulesPerCycle = 20;
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

public static class PwaRoutes
{
    public const string Manifest = "/manifest.webmanifest";
    public const string ServiceWorker = "/service-worker.js";
    public const string OfflinePage = "/offline.html";
    public const string Icon192 = "/icons/icon-192.png";
    public const string Icon512 = "/icons/icon-512.png";
    public const string Icon512Maskable = "/icons/icon-512-maskable.png";
    public const string AppleTouchIcon = "/icons/apple-touch-icon.png";
    public const string ManifestContentType = "application/manifest+json";
    public const string StartUrl = "/";
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
    public const string BrokerProbeAlgoFile = "broker-probe.algo";
    // Default web-host path to the prebuilt broker-probe .algo (source: tools/broker-probe/).
    public const string BrokerProbeAlgoDefault = "broker-probe/broker-probe.algo";
    public static readonly string BuildWorkRootDefault =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "app", "builds");
    public static readonly string LocalRunWorkRootDefault =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "app", "local-runs");
    public const string DataRootPrefix = "/var/app/";
    public const string NodeDataDirDefault = "/var/app/data";
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
