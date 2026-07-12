namespace Core.Quant;

/// <summary>
/// A fund-grade verdict on a backtest: does the edge survive correction for selection bias and the
/// number of configurations tried? Every field is deterministic and carries a plain-English
/// <see cref="Rationale"/> so the UI can always answer "why".
/// </summary>
public sealed record BacktestIntegrityReport(
    Verdict Verdict,
    double Sharpe,
    double AnnualizedSharpe,
    Probability ProbabilisticSharpe,
    Probability DeflatedSharpe,
    double TStatistic,
    double Skewness,
    double Kurtosis,
    int Observations,
    int Trials,
    Probability? ProbabilityOfBacktestOverfitting,
    string Rationale);
