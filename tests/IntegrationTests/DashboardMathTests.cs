using FluentAssertions;
using Web.Endpoints;
using Xunit;

namespace IntegrationTests;

// Pure aggregation logic behind the dashboard — deterministic, no DB, no real clock (now is passed in).
public class DashboardMathTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);
    private const int Buckets = 24;

    private static InstanceEvent Completed(TimeSpan ago) => new(Now - ago, Now - ago, null);
    private static InstanceEvent Failed(TimeSpan ago) => new(Now - ago, null, Now - ago);

    private static IReadOnlyList<InstanceEvent> Sample() =>
    [
        Completed(TimeSpan.FromHours(1)),
        Completed(TimeSpan.FromHours(2)),
        Completed(TimeSpan.FromHours(3)),
        Failed(TimeSpan.FromHours(4)),
        Completed(TimeSpan.FromHours(26)), // previous window
        Failed(TimeSpan.FromHours(27)),    // previous window
        Failed(TimeSpan.FromHours(28))     // previous window
    ];

    [Fact]
    public void BuildBuckets_produces_one_bucket_per_slot_and_counts_only_the_current_window()
    {
        var buckets = DashboardMath.BuildBuckets(Sample(), Now, Window, Buckets);

        buckets.Should().HaveCount(Buckets);
        buckets[0].Start.Should().Be(Now - Window);
        buckets.Sum(b => b.Started).Should().Be(4, "only the four current-window events were created inside the window");
        buckets.Sum(b => b.Completed).Should().Be(3);
        buckets.Sum(b => b.Failed).Should().Be(1);
    }

    [Fact]
    public void BuildKpis_computes_success_rate_and_previous_period_deltas()
    {
        var kpis = DashboardMath.BuildKpis(Sample(), activeNow: 5, Now, Window, Buckets);

        kpis.ActiveNow.Should().Be(5);
        kpis.Completed.Should().Be(3);
        kpis.Failed.Should().Be(1);
        kpis.CompletedDelta.Should().Be(2, "3 completed now vs 1 completed in the previous window");
        kpis.FailedDelta.Should().Be(-1, "1 failed now vs 2 failed in the previous window");
        kpis.SuccessRate.Should().BeApproximately(0.75, 0.0001);
        kpis.SuccessRateDelta.Should().BeApproximately(0.75 - (1d / 3d), 0.0001);
        kpis.SuccessSpark.Should().HaveCount(Buckets);
        kpis.CompletedSpark.Should().HaveCount(Buckets);
    }

    [Fact]
    public void BuildKpis_with_no_events_is_all_zero_and_never_divides_by_zero()
    {
        var kpis = DashboardMath.BuildKpis([], activeNow: 0, Now, Window, Buckets);

        kpis.Completed.Should().Be(0);
        kpis.Failed.Should().Be(0);
        kpis.SuccessRate.Should().Be(0);
        kpis.SuccessRateDelta.Should().Be(0);
    }

    [Fact]
    public void An_event_completing_exactly_at_now_lands_in_the_last_bucket()
    {
        var buckets = DashboardMath.BuildBuckets([new InstanceEvent(Now, Now, null)], Now, Window, Buckets);

        buckets[^1].Completed.Should().Be(1);
        buckets.Sum(b => b.Completed).Should().Be(1);
    }

    [Theory]
    [InlineData("1h", DashboardPeriod.Hour)]
    [InlineData("24h", DashboardPeriod.Day)]
    [InlineData("7d", DashboardPeriod.Week)]
    [InlineData("30d", DashboardPeriod.Month)]
    [InlineData(null, DashboardPeriod.Day)]
    [InlineData("garbage", DashboardPeriod.Day)]
    public void Parse_maps_wire_period_to_enum(string? wire, DashboardPeriod expected) =>
        DashboardPeriods.Parse(wire).Should().Be(expected);

    [Theory]
    [InlineData(DashboardPeriod.Hour, 12)]
    [InlineData(DashboardPeriod.Day, 24)]
    [InlineData(DashboardPeriod.Week, 7)]
    [InlineData(DashboardPeriod.Month, 30)]
    public void Plan_assigns_a_bucket_count_per_period(DashboardPeriod period, int expectedBuckets) =>
        DashboardPeriods.Plan(period).Buckets.Should().Be(expectedBuckets);
}
