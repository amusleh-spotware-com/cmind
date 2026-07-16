namespace Web.Components.Quant;

/// <summary>
/// Client-side projection of the Backtest Integrity Lab report (server: <c>IntegrityResponse</c>). Shared
/// by the Integrity page, the reusable <c>IntegrityResultView</c>, and the backtest-instance integrity
/// dialog so the verdict and statistics render identically wherever the check is run.
/// </summary>
public sealed record IntegrityResult(
    string verdict,
    double sharpe,
    double annualizedSharpe,
    double probabilisticSharpe,
    double deflatedSharpe,
    double tStatistic,
    double skewness,
    double kurtosis,
    int observations,
    int trials,
    double? probabilityOfBacktestOverfitting,
    string rationale);
