using System.Globalization;

namespace Core.Journal;

public enum TradeOutcome
{
    Running,
    Completed,
    Failed
}

/// <summary>One item in a trader's journal, derived from an instance (run or backtest).</summary>
public sealed record JournalEntry(string? Symbol, string Kind, TradeOutcome Outcome, double? NetProfit);

/// <summary>Deterministic behavioural summary of a trader's activity, with plain-English insights.</summary>
public sealed record JournalSummary(
    int Total, int Wins, int Losses, int Failures, double WinRate, IReadOnlyList<string> Insights);

public interface IJournalAnalyzer
{
    JournalSummary Analyze(IReadOnlyList<JournalEntry> entries);
}

/// <summary>
/// Analyzes a trader's own activity for behavioural leaks — over-concentration, repeated failures, a
/// losing bias — from the outcomes of their runs and backtests. Pure and deterministic; the AI coach
/// layers narrative on top of these facts.
/// </summary>
public sealed class JournalAnalyzer : IJournalAnalyzer
{
    private const double ConcentrationThreshold = 0.6;
    private const double FailureThreshold = 0.3;

    public JournalSummary Analyze(IReadOnlyList<JournalEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var total = entries.Count;
        if (total == 0)
            return new JournalSummary(0, 0, 0, 0, 0, ["Not enough trading history yet to analyze."]);

        var failures = entries.Count(e => e.Outcome == TradeOutcome.Failed);
        var wins = entries.Count(e => e.NetProfit is > 0);
        var losses = entries.Count(e => e.NetProfit is <= 0);
        var decided = wins + losses;
        var winRate = decided > 0 ? (double)wins / decided : 0.0;

        var insights = new List<string>();

        var topSymbol = entries.Where(e => !string.IsNullOrWhiteSpace(e.Symbol))
            .GroupBy(e => e.Symbol!.ToUpperInvariant())
            .Select(g => (Symbol: g.Key, Count: g.Count()))
            .OrderByDescending(g => g.Count)
            .FirstOrDefault();
        if (topSymbol.Count > 0 && (double)topSymbol.Count / total >= ConcentrationThreshold)
            insights.Add($"Over-concentrated in {topSymbol.Symbol} ({topSymbol.Count} of {total}). Consider diversifying.");

        if ((double)failures / total >= FailureThreshold)
            insights.Add($"A high share of runs failed ({failures} of {total}). Check your bot builds and configuration.");

        if (decided > 0 && losses > wins)
            insights.Add(string.Format(CultureInfo.InvariantCulture,
                "More losing than winning backtests ({0} vs {1}). Revisit whether the edge is real — run the Integrity Lab.", losses, wins));

        if (insights.Count == 0)
            insights.Add("Balanced activity with no obvious behavioural leaks — keep monitoring.");

        return new JournalSummary(total, wins, losses, failures, winRate, insights);
    }
}
