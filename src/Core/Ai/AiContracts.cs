namespace Core.Ai;

public sealed record AiImage(string MediaType, string Base64Data);

public sealed record AiTextRequest(
    string System,
    string User,
    int? MaxTokens = null,
    bool EnableWebSearch = false,
    AiImage? Image = null);

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
    Task<AiResult> AnalyzeBacktestAsync(string cBotName, string reportJson, CancellationToken ct);
    Task<AiResult> ProposeParamSetsAsync(string cBotName, string currentParamsJson, string? backtestReportJson, CancellationToken ct);
    Task<AiResult> PostMortemAsync(AiInstanceContext context, CancellationToken ct);
    Task<AiResult> AssessRiskAsync(IReadOnlyList<AiInstanceContext> running, CancellationToken ct);
    Task<AiResult> MarketSentimentAsync(string symbol, CancellationToken ct);
    Task<AiResult> VisionToStrategyAsync(AiImage chart, string? note, CancellationToken ct);
    Task<AiResult> CurateStrategyAsync(string name, string language, string source, CancellationToken ct);
}
