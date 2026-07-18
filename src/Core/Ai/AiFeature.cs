namespace Core.Ai;

/// <summary>
/// The AI capabilities the app exposes — one value per <see cref="IAiFeatureService"/> operation. A user (or
/// the deployment owner) can bind each feature to a different provider credential, so several models run
/// different features at the same time; a feature with no binding falls back to the scope's active provider.
/// Each enum name equals the corresponding <c>IAiFeatureService</c> method name without the <c>Async</c>
/// suffix; the census gate <c>AiFeatureBindingParityTests</c> fails the build if the two ever drift apart.
/// </summary>
public enum AiFeature
{
    GenerateCBot,
    ReviewCBot,
    FixCBot,
    ProposeParamSetSuite,
    AnalyzeBacktest,
    ProposeParamSets,
    PostMortem,
    AssessRisk,
    AssessRiskActions,
    MarketSentiment,
    AssessSymbolAlert,
    VisionToStrategy,
    CurateStrategy,
    ProposeAgentAction,
    AssessStrategyDecay,
    PortfolioDigest,
    DebateStrategy,
    AssessLiveExposure,
    RecommendCopyProfile,
    GatherCurrencyForward,
    ExplainCurrencyOutlook
}
