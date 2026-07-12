using Core;
using Core.Ai.CurrencyStrength;
using Core.Calendar;
using FluentAssertions;
using Infrastructure.Ai.CurrencyStrength;
using Xunit;
using static UnitTests.Ai.CurrencyStrength.CurrencyTestData;

namespace UnitTests.Ai.CurrencyStrength;

public sealed class CurrencyMacroAssemblerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Binds_latest_actual_per_driver_and_sums_surprise_momentum()
    {
        var calendar = new FakeCalendar
        {
            ["USD"] =
            [
                Event("US.POLICY.RATE", Now.AddDays(-30), 5.25m, surprise: 0.5),
                Event("US.POLICY.RATE", Now.AddDays(-90), 5.00m, surprise: 0.0),
                Event("US.CPI.YOY", Now.AddDays(-10), 3.1m, surprise: -0.4),
                Event("US.GDP.QOQ", Now.AddDays(-40), 2.4m, surprise: 0.3),
                Event("US.UNEMP.RATE", Now.AddDays(-12), 4.1m, surprise: 0.2)
            ]
        };
        var assembler = new CurrencyMacroAssembler(calendar);
        var universe = CurrencyUniverse.Of([Major("USD"), Major("EUR")]);

        var assembled = await assembler.AssembleAsync(universe, Now, CancellationToken.None);

        assembled.Should().ContainKey("USD");
        var usd = assembled["USD"];
        usd.PolicyRate.Should().Be(5.25, "the most recent rate print wins");
        usd.Cpi.Should().Be(3.1);
        usd.GdpGrowth.Should().Be(2.4);
        usd.Unemployment.Should().Be(4.1);
        usd.SurpriseMomentum.Should().BeApproximately(0.5 + 0.0 - 0.4 + 0.3 + 0.2, 1e-9);
        usd.KnownAt.Should().Be(Now);
        assembled.Should().NotContainKey("EUR", "the calendar covers no EUR events");
    }

    [Fact]
    public async Task Honours_the_point_in_time_anchor()
    {
        var calendar = new FakeCalendar();
        var assembler = new CurrencyMacroAssembler(calendar);
        var universe = CurrencyUniverse.Of([Major("USD")]);

        await assembler.AssembleAsync(universe, Now, CancellationToken.None);

        calendar.LastQuery!.AsOf.Should().Be(Now, "assembling as-of a past instant must not look ahead");
        calendar.LastQuery.To.Should().Be(Now);
    }

    private static CalendarEventView Event(string series, DateTimeOffset at, decimal actual, double surprise) =>
        new()
        {
            Id = CalendarEventId.New(),
            SeriesCode = series,
            Country = series[..2],
            EffectiveAt = at,
            SourceTimeZone = "UTC",
            Precision = ReleasePrecision.Exact,
            Impact = ImpactLevel.High,
            ImpactScore = 80,
            Released = true,
            Actual = actual,
            SurpriseZScore = surprise
        };

    private sealed class FakeCalendar : IEconomicCalendar
    {
        private readonly Dictionary<string, List<CalendarEventView>> _byCurrency = new(StringComparer.Ordinal);

        public CalendarQuery? LastQuery { get; private set; }

        public List<CalendarEventView> this[string currency]
        {
            set => _byCurrency[currency] = value;
        }

        public Task<IReadOnlyList<CalendarEventView>> GetEventsAsync(CalendarQuery query, CancellationToken ct)
        {
            LastQuery = query;
            var currency = query.Currencies?.FirstOrDefault();
            IReadOnlyList<CalendarEventView> events =
                currency is not null && _byCurrency.TryGetValue(currency, out var list) ? list : [];
            return Task.FromResult(events);
        }

        public Task<CalendarEventView?> GetEventAsync(CalendarEventId id, IReadOnlyList<string>? watchlist, DateTimeOffset? asOf, CancellationToken ct) =>
            Task.FromResult<CalendarEventView?>(null);

        public Task<IReadOnlyList<SurprisePoint>> GetSurprisesAsync(SeriesCode series, int count, DateTimeOffset? asOf, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SurprisePoint>>([]);

        public Task<IReadOnlyList<SeriesCatalogEntry>> GetSeriesAsync(CalendarQuery query, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SeriesCatalogEntry>>([]);

        public Task<CalendarEventView?> GetNextForSymbolAsync(Symbol symbol, ImpactLevel minImpact, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult<CalendarEventView?>(null);

        public Task<IReadOnlyList<CalendarEventView>> GetEventsForSymbolAsync(Symbol symbol, DateTimeOffset from, DateTimeOffset to, DateTimeOffset? asOf, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CalendarEventView>>([]);

        public Task<BlackoutResult> GetBlackoutAsync(Symbol symbol, DateTimeOffset at, NewsWindowRule rule, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Symbol>> GetAffectedSymbolsAsync(CalendarEventId id, IReadOnlyList<string> watchlist, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Symbol>>([]);

        public Task<IReadOnlyList<SourceHealth>> GetHealthAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SourceHealth>>([]);
    }
}
