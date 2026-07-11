using Core.Constants;
using Core.Domain;
using Core.PropFirm;
using FluentAssertions;
using Xunit;

namespace UnitTests.PropFirm;

public class PropFirmValueObjectTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.1)]
    public void Percent_rejects_out_of_range(double value)
    {
        var act = () => new Percent(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.PropFirmPercentOutOfRange);
    }

    [Fact]
    public void Percent_fraction_is_value_over_hundred() => new Percent(10).Fraction.Should().Be(0.10m);

    [Fact]
    public void Money_rejects_negative()
    {
        var act = () => new Money(-1m);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.PropFirmMoneyNegative);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void TradingDayRequirement_rejects_out_of_range(int value)
    {
        var act = () => new TradingDayRequirement(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.PropFirmTradingDaysOutOfRange);
    }

    [Fact]
    public void TrailingThreshold_rejects_non_positive_trail_amount()
    {
        var act = () => DrawdownLimit.TrailingThreshold(0m, 100m);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.PropFirmDrawdownThresholdInvalid);
    }

    [Fact]
    public void Static_drawdown_breaches_from_starting_balance()
    {
        var limit = DrawdownLimit.Static(new Percent(10));
        limit.IsBreached(100_000m, 100_000m, 90_000m).Should().BeTrue();
        limit.IsBreached(100_000m, 100_000m, 91_000m).Should().BeFalse();
    }

    [Fact]
    public void Trailing_drawdown_breaches_from_peak_equity()
    {
        var limit = DrawdownLimit.Trailing(new Percent(10));
        limit.IsBreached(100_000m, 120_000m, 108_000m).Should().BeTrue();
        limit.IsBreached(100_000m, 120_000m, 109_000m).Should().BeFalse();
    }

    [Fact]
    public void Daily_loss_limit_uses_the_configured_basis()
    {
        var balance = new DailyLossLimit(new Percent(5), DailyLossBasis.Balance);
        balance.IsBreached(100_000m, 90_000m, 100_000m, 100_000m).Should().BeFalse();
        balance.IsBreached(100_000m, 100_000m, 100_000m, 94_000m).Should().BeTrue();
    }

    [Fact]
    public void Consistency_rule_is_satisfied_when_no_profit_yet()
    {
        var rule = new ConsistencyRule(new Percent(40));
        rule.IsSatisfied(0m, 0m).Should().BeTrue();
        rule.IsSatisfied(5_000m, 20_000m).Should().BeTrue();
        rule.IsSatisfied(9_000m, 20_000m).Should().BeFalse();
    }
}
