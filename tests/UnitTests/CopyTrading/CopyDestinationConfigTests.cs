using Core;
using Core.Constants;
using Core.CopyTrading;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

// The performance-fee high-water-mark settlement model on CopyDestination, plus the config-value guards
// and the config lock. (WS-1 Core backfill.)
public class CopyDestinationConfigTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 19, 0, 0, TimeSpan.Zero);

    private static CopyDestination NewDestination()
        => CopyProfile.Create(UserId.New(), "p", TradingAccountId.New())
            .AddDestination(TradingAccountId.New(), RiskSettings.Default);

    [Fact]
    public void Settle_fee_is_zero_when_no_fee_is_configured()
    {
        var dest = NewDestination();
        dest.SettleFee(1000).Should().Be(0);
    }

    [Fact]
    public void Settle_fee_follows_the_high_water_mark_model()
    {
        var dest = NewDestination();
        dest.SetPerformanceFee(new PerformanceFee(20));

        dest.SettleFee(1000).Should().Be(0, "the first settlement seeds the HWM at the opening equity");
        dest.SettleFee(900).Should().Be(0, "below the peak nothing is charged");
        dest.SettleFee(1000).Should().Be(0, "at the peak nothing is charged");
        dest.SettleFee(1200).Should().Be(40, "20% of the 200 gained above the peak");
        dest.SettleFee(1300).Should().Be(20, "20% of the next 100 above the advanced peak");
        dest.SettleFee(1300).Should().Be(0, "no new high, no fee");
    }

    [Fact]
    public void Consistency_threshold_and_execution_jitter_reject_negatives()
    {
        var dest = NewDestination();

        var badThreshold = () => dest.SetConsistencyThreshold(-1);
        badThreshold.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CopyRiskParameterInvalid);

        var badJitter = () => dest.SetExecutionJitter(-1);
        badJitter.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CopyRiskParameterInvalid);

        dest.SetConsistencyThreshold(50);
        dest.SetExecutionJitter(250); // valid values do not throw
    }

    [Fact]
    public void Config_lock_reports_locked_until_it_expires()
    {
        var dest = NewDestination();

        dest.IsConfigLocked(Now).Should().BeFalse();

        dest.LockConfig(Now.AddHours(1));
        dest.IsConfigLocked(Now).Should().BeTrue();
        dest.IsConfigLocked(Now.AddHours(2)).Should().BeFalse("the lock has expired");
    }
}
