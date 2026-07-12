using System.Text.Json;
using Core.Ai.CurrencyStrength;
using Core.Calendar;

namespace Infrastructure.Ai.CurrencyStrength;

/// <summary>
/// Binds each currency's CURRENT macro figures from the Economic Calendar's point-in-time read model (latest
/// released actual per driver + a summed surprise z-score for momentum), so the deterministic engine anchors on
/// real authority-sourced data rather than the LLM. Honours the <c>asOf</c> anchor — assembling as of a past
/// instant uses only then-known releases (no look-ahead). A currency the calendar does not cover falls back to
/// the AI gap-fill downstream (<see cref="CurrencyMacroBinder"/>).
/// </summary>
public sealed class CurrencyMacroAssembler(IEconomicCalendar calendar)
{
    private const int LookbackDays = 400;

    public async Task<IReadOnlyDictionary<string, CurrencyMacroInputs>> AssembleAsync(
        CurrencyUniverse universe, DateTimeOffset asOf, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(universe);
        var result = new Dictionary<string, CurrencyMacroInputs>(StringComparer.Ordinal);

        foreach (var currency in universe.Currencies)
        {
            var events = await calendar.GetEventsAsync(new CalendarQuery
            {
                Currencies = [currency.Code],
                From = asOf.AddDays(-LookbackDays),
                To = asOf,
                AsOf = asOf,
                MinImpact = ImpactLevel.Low,
                Limit = 300
            }, ct);

            var released = events
                .Where(e => e.Released && e.Actual is not null)
                .OrderByDescending(e => e.EffectiveAt)
                .ToList();
            if (released.Count == 0) continue;

            // Classify each release to at most one driver (priority order disambiguates codes such as
            // "US.UNEMP.RATE"); the first assignment in descending-time order is the latest print.
            var latest = new Dictionary<MacroDriver, double>();
            foreach (var e in released)
            {
                var driver = Classify(e.SeriesCode);
                if (driver is { } d && e.Actual is { } actual)
                    latest.TryAdd(d, (double)actual);
            }

            var inputs = new CurrencyMacroInputs
            {
                PolicyRate = latest.TryGetValue(MacroDriver.PolicyRate, out var rate) ? rate : null,
                Cpi = latest.TryGetValue(MacroDriver.Inflation, out var cpi) ? cpi : null,
                GdpGrowth = latest.TryGetValue(MacroDriver.GdpGrowth, out var gdp) ? gdp : null,
                Unemployment = latest.TryGetValue(MacroDriver.Employment, out var unemp) ? unemp : null,
                TradeBalancePercentGdp = latest.TryGetValue(MacroDriver.TradeBalance, out var trade) ? trade : null,
                SurpriseMomentum = released.Sum(e => e.SurpriseZScore),
                Confidence = ConfidenceFor(currency.Tier),
                KnownAt = asOf
            };

            result[currency.Code] = inputs;
        }

        return result;
    }

    /// <summary>A compact per-currency JSON of the calendar-sourced actuals, fed to the AI so it anchors its
    /// forward view on real figures rather than re-guessing them.</summary>
    public static string BuildContextJson(
        CurrencyUniverse universe, IReadOnlyDictionary<string, CurrencyMacroInputs> assembled)
    {
        var payload = universe.Currencies.Select(c =>
        {
            assembled.TryGetValue(c.Code, out var i);
            return new
            {
                code = c.Code,
                tier = c.Tier.ToString(),
                pegged = c.IsPegged,
                policyRate = i?.PolicyRate,
                cpi = i?.Cpi,
                gdpGrowth = i?.GdpGrowth,
                unemployment = i?.Unemployment,
                tradeBalance = i?.TradeBalancePercentGdp,
                surpriseMomentum = i?.SurpriseMomentum
            };
        });

        return JsonSerializer.Serialize(new { currencies = payload });
    }

    /// <summary>Maps a series code to at most one driver. Employment is checked before the policy rate so a
    /// code such as <c>US.UNEMP.RATE</c> is not mistaken for a rate decision.</summary>
    private static MacroDriver? Classify(string seriesCode)
    {
        var code = seriesCode.ToUpperInvariant();
        if (Has(code, "UNEMP", "JOBLESS", "PAYROLL", "NFP")) return MacroDriver.Employment;
        if (Has(code, "CPI", "INFLATION", "PCE", "HICP")) return MacroDriver.Inflation;
        if (Has(code, "GDP")) return MacroDriver.GdpGrowth;
        if (Has(code, "TRADE", "CURRENTACCOUNT")) return MacroDriver.TradeBalance;
        if (Has(code, "RATE", "POLICY", "REFI", "BANKRATE")) return MacroDriver.PolicyRate;
        return null;
    }

    private static bool Has(string code, params string[] tokens)
    {
        foreach (var token in tokens)
            if (code.Contains(token, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static DataConfidence ConfidenceFor(CurrencyTier tier) => tier switch
    {
        CurrencyTier.Major => DataConfidence.High,
        CurrencyTier.EmergingMarket => DataConfidence.Medium,
        _ => DataConfidence.Low
    };
}
