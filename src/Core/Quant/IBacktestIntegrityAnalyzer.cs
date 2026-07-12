namespace Core.Quant;

/// <summary>
/// Turns a return series plus the number of configurations tried into a <see cref="BacktestIntegrityReport"/>.
/// Pure and deterministic — a domain service with no infrastructure dependency.
/// </summary>
public interface IBacktestIntegrityAnalyzer
{
    /// <summary>
    /// Analyzes a single backtest. <paramref name="trials"/> is how many parameter sets were tried to
    /// arrive at this one (drives the deflation); <paramref name="benchmarkSharpe"/> is the per-observation
    /// Sharpe the strategy must beat to count as skill (default 0). <paramref name="periodsPerYear"/> only
    /// scales the displayed annualized Sharpe.
    /// </summary>
    BacktestIntegrityReport Analyze(
        ReturnSeries returns,
        TrialCount trials,
        double benchmarkSharpe = 0.0,
        double periodsPerYear = 252.0);

    /// <summary>
    /// Analyzes a whole optimization surface: reports the best trial's statistics plus the Probability of
    /// Backtest Overfitting (Combinatorially-Symmetric Cross-Validation) across all trials — judging the
    /// selection process, not just the winner.
    /// </summary>
    BacktestIntegrityReport AnalyzeGrid(
        TrialSurface surface,
        int slices = 8,
        double benchmarkSharpe = 0.0,
        double periodsPerYear = 252.0);
}
