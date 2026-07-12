using Core;
using Core.Calendar;
using FluentAssertions;
using Nodes;
using Xunit;

namespace UnitTests.Calendar;

public sealed class NewsBlackoutRiskFilterTests
{
    // Minimal IEconomicCalendar: only the blackout path is exercised; EURUSD is "in a window".
    private sealed class FakeCalendar : IEconomicCalendar
    {
        public Task<BlackoutResult> GetBlackoutAsync(Symbol symbol, DateTimeOffset at, NewsWindowRule rule, CancellationToken ct)
            => Task.FromResult(symbol.Value == "EURUSD"
                ? new BlackoutResult(true, null, at, at)
                : BlackoutResult.Clear);

        public Task<IReadOnlyList<CalendarEventView>> GetEventsAsync(CalendarQuery query, CancellationToken ct) => throw new NotSupportedException();
        public Task<CalendarEventView?> GetEventAsync(CalendarEventId id, IReadOnlyList<string>? watchlist, DateTimeOffset? asOf, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SurprisePoint>> GetSurprisesAsync(SeriesCode series, int count, DateTimeOffset? asOf, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SeriesCatalogEntry>> GetSeriesAsync(CalendarQuery query, CancellationToken ct) => throw new NotSupportedException();
        public Task<CalendarEventView?> GetNextForSymbolAsync(Symbol symbol, ImpactLevel minImpact, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Symbol>> GetAffectedSymbolsAsync(CalendarEventId id, IReadOnlyList<string> watchlist, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SourceHealth>> GetHealthAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Flags_only_symbols_inside_a_blackout_deduplicated_case_insensitively()
    {
        var filter = new NewsBlackoutRiskFilter(new FakeCalendar(), TimeProvider.System);

        var flagged = await filter.SymbolsInBlackoutAsync(
            ["EURUSD", "USDJPY", "eurusd", "  "], CancellationToken.None);

        flagged.Should().ContainSingle().Which.Should().Be("EURUSD");
    }

    [Fact]
    public async Task Returns_empty_when_nothing_is_in_a_window()
    {
        var filter = new NewsBlackoutRiskFilter(new FakeCalendar(), TimeProvider.System);

        (await filter.SymbolsInBlackoutAsync(["USDJPY", "GBPUSD"], CancellationToken.None))
            .Should().BeEmpty();
    }
}
