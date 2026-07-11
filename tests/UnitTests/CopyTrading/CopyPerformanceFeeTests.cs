using Core;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

// Phase 4 performance fee (high-water-mark model). The fee arithmetic is a domain invariant on
// CopyDestination.SettleFee: a fee accrues only on equity above the follower's peak, the peak never
// retreats, and the first settlement seeds the peak (no charge on the opening balance).
public sealed class CopyPerformanceFeeTests
{
    private static CopyDestination FeeDestination(double feePercent)
    {
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        var destination = profile.AddDestination(TradingAccountId.New(), RiskSettings.Default);
        destination.SetPerformanceFee(new PerformanceFee(feePercent));
        return destination;
    }

    [Fact]
    public void First_settlement_seeds_the_high_water_mark_and_charges_nothing()
    {
        var destination = FeeDestination(20);

        var fee = destination.SettleFee(10_000);

        fee.Should().Be(0, "the opening balance is never charged");
        destination.HighWaterMarkEquity.Should().Be(10_000);
    }

    [Fact]
    public void A_new_high_charges_the_fee_on_the_gain_and_advances_the_mark()
    {
        var destination = FeeDestination(20);
        destination.SettleFee(10_000); // seed

        var fee = destination.SettleFee(12_000);

        fee.Should().Be(400, "20% of the 2000 gain above the peak");
        destination.HighWaterMarkEquity.Should().Be(12_000);
    }

    [Fact]
    public void At_or_below_the_peak_charges_nothing_and_leaves_the_mark_untouched()
    {
        var destination = FeeDestination(20);
        destination.SettleFee(10_000);
        destination.SettleFee(12_000); // peak = 12000

        destination.SettleFee(11_000).Should().Be(0, "below the peak — no fee");
        destination.SettleFee(12_000).Should().Be(0, "exactly at the peak — still no fee");
        destination.HighWaterMarkEquity.Should().Be(12_000, "the peak never retreats");
    }

    [Fact]
    public void After_a_drawdown_only_the_recovery_past_the_old_peak_is_charged()
    {
        var destination = FeeDestination(20);
        destination.SettleFee(10_000);
        destination.SettleFee(12_000); // peak = 12000
        destination.SettleFee(9_000);  // drawdown — no fee, peak stays 12000

        destination.SettleFee(11_500).Should().Be(0, "still recovering below the old peak");
        var fee = destination.SettleFee(12_500);

        fee.Should().Be(100, "only the 500 above the old 12000 peak is charged, not the whole recovery");
        destination.HighWaterMarkEquity.Should().Be(12_500);
    }

    [Fact]
    public void No_fee_configured_never_charges_and_never_seeds_a_mark()
    {
        var destination = FeeDestination(0);

        destination.SettleFee(10_000).Should().Be(0);
        destination.SettleFee(20_000).Should().Be(0);
        destination.HighWaterMarkEquity.Should().Be(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(60)]
    [InlineData(double.NaN)]
    public void Performance_fee_rejects_an_out_of_range_percent(double percent)
    {
        var act = () => new PerformanceFee(percent);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Performance_fee_accepts_a_sane_percent()
        => new PerformanceFee(25).Percent.Should().Be(25);
}
