namespace Core.Regimes;

/// <summary>A volatility regime a stretch of the market was in.</summary>
public enum MarketRegime
{
    /// <summary>Low realized volatility.</summary>
    Calm,

    /// <summary>Typical realized volatility.</summary>
    Normal,

    /// <summary>High realized volatility (stress).</summary>
    Turbulent
}

/// <summary>A contiguous run of observations sharing one regime.</summary>
public sealed record RegimeLabel(int StartIndex, int EndIndex, MarketRegime Regime);

/// <summary>Aggregate performance of a strategy while the market was in a given regime.</summary>
public sealed record RegimePerformance(
    MarketRegime Regime, int Observations, double MeanReturn, double Volatility, double Sharpe);

/// <summary>
/// A regime breakdown of a return series: where each regime was, how the strategy performed in each,
/// and the Hurst exponent (trend-persistence: &gt; 0.5 trending, &lt; 0.5 mean-reverting). Deterministic.
/// </summary>
public sealed record RegimeAnalysis(
    IReadOnlyList<RegimeLabel> Labels,
    IReadOnlyList<RegimePerformance> ByRegime,
    double HurstExponent,
    string Rationale);
