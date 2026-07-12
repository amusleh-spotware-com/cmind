using Infrastructure.Calendar;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

public sealed class CentralBankScheduleSourceTests
{
    private static DateTimeOffset Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, TimeSpan.Zero);
    private readonly CentralBankScheduleSource _source = new();

    [Fact]
    public async Task Returns_the_fomc_meetings_within_a_window()
    {
        var year = await _source.FetchScheduleAsync("FOMC", Utc(2025, 1, 1), Utc(2025, 12, 31), CancellationToken.None);
        year.Should().HaveCount(8);
        year.Should().OnlyContain(i => i.Window.Precision == Core.Calendar.ReleasePrecision.Exact);
    }

    [Fact]
    public async Task Filters_to_the_requested_range_case_insensitively()
    {
        var june = await _source.FetchScheduleAsync("fomc", Utc(2025, 6, 1), Utc(2025, 7, 1), CancellationToken.None);
        june.Should().ContainSingle();
        june[0].Window.Instant.Should().Be(new DateTimeOffset(2025, 6, 18, 19, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Unknown_series_and_release_fetch_are_empty()
    {
        (await _source.FetchScheduleAsync("UNKNOWN", Utc(2025, 1, 1), Utc(2026, 1, 1), CancellationToken.None))
            .Should().BeEmpty();
        (await _source.FetchReleasesAsync("FOMC", Utc(2025, 1, 1), Utc(2026, 1, 1), CancellationToken.None))
            .Should().BeEmpty();
    }
}
