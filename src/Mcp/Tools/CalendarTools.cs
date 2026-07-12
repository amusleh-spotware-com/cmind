using System.ComponentModel;
using System.Globalization;
using Core;
using Core.Calendar;
using ModelContextProtocol.Server;

namespace Mcp.Tools;

/// <summary>
/// The economic calendar as MCP tools — full parity with the REST read API so every AI client and in-app AI
/// feature gets the same power a cBot has. Read-only, point-in-time correct (pass <c>asOf</c> to see the
/// calendar as it stood at a past instant), and served through the same <see cref="IEconomicCalendar"/>
/// domain read side as the REST API. Streaming/webhooks/token-issue are REST-only.
/// </summary>
[McpServerToolType]
public sealed class CalendarTools(IEconomicCalendar calendar, TimeProvider timeProvider)
{
    [McpServerTool, Description(
        "Economic events in a window (upcoming or historical). Filters are comma-separated. Pass asOf " +
        "(ISO-8601) for a point-in-time view with no look-ahead.")]
    public async Task<object> CalendarEvents(
        [Description("Window start, ISO-8601 (optional)")] string? from = null,
        [Description("Window end, ISO-8601 (optional)")] string? to = null,
        [Description("Comma-separated ISO country codes, e.g. US,DE (optional)")] string? countries = null,
        [Description("Comma-separated currencies, e.g. USD,EUR (optional)")] string? currencies = null,
        [Description("Comma-separated series codes (optional)")] string? series = null,
        [Description("Minimum impact: Low, Medium, High, Critical (optional)")] string? minImpact = null,
        [Description("Keyword search (optional)")] string? q = null,
        [Description("Point-in-time anchor, ISO-8601 (optional)")] string? asOf = null,
        [Description("Max results (default 200)")] int limit = 200)
    {
        var query = new CalendarQuery
        {
            From = Instant(from),
            To = Instant(to),
            Countries = Csv(countries),
            Currencies = Csv(currencies),
            Series = Csv(series),
            MinImpact = Impact(minImpact),
            Keyword = string.IsNullOrWhiteSpace(q) ? null : q,
            AsOf = Instant(asOf),
            Limit = Math.Clamp(limit, 1, 1000)
        };
        return await calendar.GetEventsAsync(query, CancellationToken.None);
    }

    [McpServerTool, Description(
        "One event with its full revision chain, surprise, impact rationale and (with a watchlist) affected symbols.")]
    public async Task<object?> CalendarEvent(
        [Description("Event ID")] Guid eventId,
        [Description("Comma-separated watchlist symbols (optional)")] string? watchlist = null,
        [Description("Point-in-time anchor, ISO-8601 (optional)")] string? asOf = null)
        => await calendar.GetEventAsync(CalendarEventId.From(eventId), Csv(watchlist), Instant(asOf), CancellationToken.None);

    [McpServerTool, Description("Deep historical events for a single series (>=10 years).")]
    public async Task<object> CalendarHistory(
        [Description("Series code, e.g. US.CPI.MoM")] string series,
        [Description("Window start, ISO-8601 (optional)")] string? from = null,
        [Description("Window end, ISO-8601 (optional)")] string? to = null,
        [Description("Point-in-time anchor, ISO-8601 (optional)")] string? asOf = null)
    {
        var query = new CalendarQuery
        {
            Series = [series],
            From = Instant(from),
            To = Instant(to),
            AsOf = Instant(asOf),
            Limit = 1000
        };
        return await calendar.GetEventsAsync(query, CancellationToken.None);
    }

    [McpServerTool, Description("Catalog of tracked indicators with cadence and source.")]
    public async Task<object> CalendarSeries(
        [Description("Comma-separated ISO country codes (optional)")] string? countries = null,
        [Description("Keyword search (optional)")] string? q = null)
        => await calendar.GetSeriesAsync(
            new CalendarQuery { Countries = Csv(countries), Keyword = string.IsNullOrWhiteSpace(q) ? null : q },
            CancellationToken.None);

    [McpServerTool, Description("Actual/forecast/surprise z-score history for a series, for reasoning or backtests.")]
    public async Task<object> CalendarSurprises(
        [Description("Series code")] string series,
        [Description("Number of recent prints (default 24)")] int count = 24)
        => await calendar.GetSurprisesAsync(new SeriesCode(series), Math.Clamp(count, 1, 500), null, CancellationToken.None);

    [McpServerTool, Description("The next relevant release for a symbol (country-mapped).")]
    public async Task<object?> CalendarNext(
        [Description("Symbol, e.g. EURUSD")] string symbol,
        [Description("Minimum impact: Low, Medium, High, Critical")] string? minImpact = null)
        => await calendar.GetNextForSymbolAsync(
            new Symbol(symbol), Impact(minImpact) ?? ImpactLevel.Low, timeProvider.GetUtcNow(), CancellationToken.None);

    [McpServerTool, Description(
        "Whether a symbol is inside a high-impact news window now or at an instant. Returns the blackout flag, " +
        "the triggering event and the window edges; defaults to the conservative answer on uncertainty.")]
    public async Task<object> CalendarBlackout(
        [Description("Symbol, e.g. EURUSD")] string symbol,
        [Description("Instant to test, ISO-8601 (default now)")] string? at = null,
        [Description("Minimum impact: Low, Medium, High, Critical (default High)")] string? minImpact = null,
        [Description("Minutes before the release (default 15)")] int before = 15,
        [Description("Minutes after the release (default 15)")] int after = 15)
    {
        var rule = new NewsWindowRule(Impact(minImpact) ?? ImpactLevel.High, Math.Max(0, before), Math.Max(1, after));
        var instant = Instant(at) ?? timeProvider.GetUtcNow();
        return await calendar.GetBlackoutAsync(new Symbol(symbol), instant, rule, CancellationToken.None);
    }

    [McpServerTool, Description("Resolve an event to the symbols in a watchlist it affects (country->currency->symbol).")]
    public async Task<object> CalendarAffectedSymbols(
        [Description("Event ID")] Guid eventId,
        [Description("Comma-separated watchlist symbols")] string watchlist)
    {
        var symbols = await calendar.GetAffectedSymbolsAsync(
            CalendarEventId.From(eventId), Csv(watchlist) ?? [], CancellationToken.None);
        return symbols.Select(s => s.Value).ToList();
    }

    [McpServerTool, Description("Per-source freshness and coverage so an agent can judge how far to trust the data.")]
    public async Task<object> CalendarHealth()
        => await calendar.GetHealthAsync(CancellationToken.None);

    private static string[]? Csv(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static DateTimeOffset? Instant(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var instant)
            ? instant
            : null;

    private static ImpactLevel? Impact(string? value) =>
        Enum.TryParse<ImpactLevel>(value, ignoreCase: true, out var level) ? level : null;
}
