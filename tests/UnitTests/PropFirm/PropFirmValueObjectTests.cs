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
}
