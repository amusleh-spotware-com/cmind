namespace Core.Ai;

public sealed record AiImage(string MediaType, string Base64Data);

public sealed record AiTextRequest(
    string System,
    string User,
    int? MaxTokens = null,
    bool EnableWebSearch = false,
    AiImage? Image = null,
    // Which AI feature this request serves — used to route to the per-feature bound provider (falls back to
    // the scope's active provider when null or unbound). Stamped by AiFeatureService per operation.
    AiFeature? Feature = null,
    // Forces a specific provider credential regardless of bindings — the async-task path uses this to run a
    // feature on a user-chosen model. Null = resolve by Feature/active as usual.
    Core.AiProviderCredentialId? CredentialId = null);

public sealed record AiResult(bool Success, string Text, string? Error = null)
{
    public static AiResult Ok(string text) => new(true, text);
    public static AiResult Fail(string error) => new(false, string.Empty, error);
}

public sealed record AiInstanceContext(
    string CBotName,
    string Kind,
    string Status,
    string? Symbol,
    string? Timeframe,
    string? Detail);

public interface IAiClient
{
    bool Enabled { get; }
    Task<AiResult> CompleteAsync(AiTextRequest request, CancellationToken ct);
}

public interface IAiFeatureService
{
    bool Enabled { get; }
    Task<AiResult> GenerateCBotAsync(string language, string description, CancellationToken ct);
    Task<AiResult> ReviewCBotAsync(string language, string source, CancellationToken ct);
    Task<AiResult> FixCBotAsync(string language, string source, string buildLog, CancellationToken ct);
    Task<AiResult> ProposeParamSetSuiteAsync(string cBotName, string currentParamsJson, int count, CancellationToken ct);
    Task<AiResult> AnalyzeBacktestAsync(string cBotName, string reportJson, CancellationToken ct);
    Task<AiResult> ProposeParamSetsAsync(string cBotName, string currentParamsJson, string? backtestReportJson, CancellationToken ct);
    Task<AiResult> PostMortemAsync(AiInstanceContext context, CancellationToken ct);
    Task<AiResult> AssessRiskAsync(IReadOnlyList<AiInstanceContext> running, CancellationToken ct);
    Task<AiResult> AssessRiskActionsAsync(IReadOnlyList<AiInstanceContext> running, int maxTokens, CancellationToken ct);
    Task<AiResult> MarketSentimentAsync(string symbol, CancellationToken ct);
    Task<AiResult> AssessSymbolAlertAsync(string symbol, int maxTokens, CancellationToken ct);
    Task<AiResult> VisionToStrategyAsync(AiImage chart, string? note, CancellationToken ct);
    Task<AiResult> CurateStrategyAsync(string name, string language, string source, CancellationToken ct);
    Task<AiResult> ProposeAgentActionAsync(string cBotName, string objective, string currentParamsJson, string? lastReportJson, int maxTokens, CancellationToken ct);
    Task<AiResult> AssessStrategyDecayAsync(string cBotName, string? previousReportJson, string latestReportJson, string currentParamsJson, int maxTokens, CancellationToken ct);
    Task<AiResult> PortfolioDigestAsync(IReadOnlyList<AiInstanceContext> portfolio, int maxTokens, CancellationToken ct);
    Task<AiResult> DebateStrategyAsync(string name, string language, string source, int maxTokens, CancellationToken ct);
    Task<AiResult> AssessLiveExposureAsync(IReadOnlyList<AiInstanceContext> live, int maxTokens, CancellationToken ct);
    Task<AiResult> RecommendCopyProfileAsync(string riskProfile, string sourceDescription, CancellationToken ct);

    /// <summary>Gathers ONLY what the calendar can't publish: each currency's forward trajectory (expected
    /// rate path, inflation trend, growth momentum, geopolitical delta) plus any EM/exotic current figures the
    /// calendar lacks. <paramref name="calendarContextJson"/> anchors the model on real point-in-time actuals.
    /// Strict JSON-only output; web search on.</summary>
    Task<AiResult> GatherCurrencyForwardAsync(string calendarContextJson, int maxTokens, CancellationToken ct);

    /// <summary>Narrates the already-computed ranking + pair-outlook matrix in plain English ("why #1 / why
    /// EUR/USD bullish 3M"). The AI never invents a rank or number — the math is deterministic in Core.</summary>
    Task<AiResult> ExplainCurrencyOutlookAsync(string rankingJson, string pairOutlookJson, int maxTokens, CancellationToken ct);
}
