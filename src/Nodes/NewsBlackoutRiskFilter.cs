using Core;
using Core.Calendar;

namespace Nodes;

/// <summary>
/// Deterministic news-window gate for the AI risk guard: which running-bot symbols are currently inside a
/// Critical-impact economic-calendar blackout. No AI call — it is the same shared <see cref="NewsWindowPolicy"/>
/// blackout math the cBot news filter uses, resolved through the calendar read side. A data gap fails to the
/// conservative answer (per the reader), so the guard never under-reports risk around a release.
/// </summary>
public sealed class NewsBlackoutRiskFilter(IEconomicCalendar calendar, TimeProvider timeProvider)
{
    private static readonly NewsWindowRule Rule = new(ImpactLevel.Critical, beforeMinutes: 30, afterMinutes: 30);

    public async Task<IReadOnlyList<string>> SymbolsInBlackoutAsync(
        IEnumerable<string?> symbols, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var flagged = new List<string>();

        foreach (var symbol in symbols
                     .Where(s => !string.IsNullOrWhiteSpace(s))
                     .Select(s => s!)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var result = await calendar.GetBlackoutAsync(new Symbol(symbol), now, Rule, ct);
            if (result.InBlackout) flagged.Add(symbol);
        }

        return flagged;
    }
}
