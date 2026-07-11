using CTraderOpenApi.Client;
using FluentAssertions;
using Xunit;

namespace UnitTests.PropFirm;

public class PropFirmEquityCalculatorTests
{
    private readonly PropFirmEquityCalculator _calc = new();

    // 1 lot EURUSD = 100_000 units -> wire volume 10_000_000.
    private const long OneLot = 10_000_000;

    [Fact]
    public void No_positions_equity_equals_balance()
    {
        var result = _calc.Compute(100_000, [], new Dictionary<long, SymbolPricing>());

        result.Equity.Should().Be(100_000);
        result.FloatingPnL.Should().Be(0);
    }

    [Fact]
    public void Long_position_profits_when_bid_rises()
    {
        var positions = new[] { new PositionValuation(1, 10, IsBuy: true, OneLot, EntryPrice: 1.1000, 0, 0) };
        var pricing = new Dictionary<long, SymbolPricing> { [10] = new(10, Bid: 1.1050, Ask: 1.1052) };

        var result = _calc.Compute(100_000, positions, pricing);

        // (1.1050 - 1.1000) * 100_000 units = 500.
        result.FloatingPnL.Should().BeApproximately(500, 1e-6);
        result.Equity.Should().BeApproximately(100_500, 1e-6);
    }

    [Fact]
    public void Short_position_profits_when_ask_falls()
    {
        var positions = new[] { new PositionValuation(1, 10, IsBuy: false, OneLot, EntryPrice: 1.1000, 0, 0) };
        var pricing = new Dictionary<long, SymbolPricing> { [10] = new(10, Bid: 1.0948, Ask: 1.0950) };

        var result = _calc.Compute(100_000, positions, pricing);

        // (1.1000 - 1.0950) * 100_000 = 500.
        result.FloatingPnL.Should().BeApproximately(500, 1e-6);
    }

    [Fact]
    public void Swap_and_commission_are_included()
    {
        var positions = new[] { new PositionValuation(1, 10, IsBuy: true, OneLot, EntryPrice: 1.1000, Swap: -3.5, Commission: -7.0) };
        var pricing = new Dictionary<long, SymbolPricing> { [10] = new(10, Bid: 1.1000, Ask: 1.1002) };

        var result = _calc.Compute(100_000, positions, pricing);

        result.FloatingPnL.Should().BeApproximately(-10.5, 1e-6);
    }

    [Fact]
    public void Quote_to_deposit_rate_converts_pnl()
    {
        // Quote currency P&L of 500 units of quote, converted at 0.9 -> 450 deposit.
        var positions = new[] { new PositionValuation(1, 10, IsBuy: true, OneLot, EntryPrice: 1.1000, 0, 0) };
        var pricing = new Dictionary<long, SymbolPricing> { [10] = new(10, Bid: 1.1050, Ask: 1.1052, QuoteToDepositRate: 0.9) };

        var result = _calc.Compute(100_000, positions, pricing);

        result.FloatingPnL.Should().BeApproximately(450, 1e-6);
    }

    [Fact]
    public void Positions_without_pricing_are_skipped()
    {
        var positions = new[] { new PositionValuation(1, 99, IsBuy: true, OneLot, EntryPrice: 1.1000, 0, 0) };

        var result = _calc.Compute(100_000, positions, new Dictionary<long, SymbolPricing>());

        result.Equity.Should().Be(100_000);
    }
}
