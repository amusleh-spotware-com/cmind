using System.Collections.Generic;
using Core.Domain;
using Core.Execution;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class TransactionCostAnalyzerTests
{
    private readonly TransactionCostAnalyzer _analyzer = new();

    [Fact]
    public void Buy_above_arrival_is_a_positive_cost()
    {
        var r = _analyzer.Analyze(1.1000, OrderSide.Buy,
            new List<Fill> { new(1.1010, 100), new(1.1020, 100) });

        r.AverageFillPrice.Should().BeApproximately(1.1015, 1e-9);
        r.FilledQuantity.Should().Be(200);
        r.SlippageBps.Should().BeApproximately(0.0015 / 1.1000 * 10000, 1e-6);
        r.ImplementationShortfall.Should().BeApproximately(0.0015 * 200, 1e-9);
    }

    [Fact]
    public void Sell_below_arrival_is_a_positive_cost()
    {
        var r = _analyzer.Analyze(1.1000, OrderSide.Sell, new List<Fill> { new(1.0990, 100) });
        r.SlippageBps.Should().BeApproximately(0.0010 / 1.1000 * 10000, 1e-6);
        r.ImplementationShortfall.Should().BeApproximately(0.0010 * 100, 1e-9);
    }

    [Fact]
    public void Filling_at_arrival_has_zero_slippage()
    {
        var r = _analyzer.Analyze(1.1000, OrderSide.Buy, new List<Fill> { new(1.1000, 100) });
        r.SlippageBps.Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public void Price_improvement_is_a_negative_cost()
    {
        var r = _analyzer.Analyze(1.1000, OrderSide.Buy, new List<Fill> { new(1.0990, 100) });
        r.SlippageBps.Should().BeLessThan(0.0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Rejects_non_positive_arrival(double arrival)
    {
        var act = () => _analyzer.Analyze(arrival, OrderSide.Buy, new List<Fill> { new(1.10, 100) });
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.execution.input_invalid");
    }

    [Fact]
    public void Rejects_empty_or_invalid_fills()
    {
        var empty = () => _analyzer.Analyze(1.10, OrderSide.Buy, new List<Fill>());
        empty.Should().Throw<DomainException>().Which.Code.Should().Be("domain.execution.input_invalid");

        var badQty = () => _analyzer.Analyze(1.10, OrderSide.Buy, new List<Fill> { new(1.10, 0) });
        badQty.Should().Throw<DomainException>().Which.Code.Should().Be("domain.execution.input_invalid");
    }

    [Theory]
    [InlineData(-100.0)]
    [InlineData(-0.5)]
    public void Rejects_negative_fill_quantity(double quantity)
    {
        var act = () => _analyzer.Analyze(1.10, OrderSide.Buy, new List<Fill> { new(1.1005, quantity) });
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.execution.input_invalid");
    }

    [Fact]
    public void Rejects_negative_fill_price()
    {
        var act = () => _analyzer.Analyze(1.10, OrderSide.Buy, new List<Fill> { new(-1.10, 100) });
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.execution.input_invalid");
    }
}
