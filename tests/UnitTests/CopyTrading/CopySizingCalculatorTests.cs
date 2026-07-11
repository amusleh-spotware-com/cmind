using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class CopySizingCalculatorTests
{
    private static readonly SymbolSpec Standard = new(ContractSize: 100000, LotStep: 0.01, MinLot: 0.01, MaxLot: 0);
    private static readonly AccountSnapshot Ten = new(Balance: 10000, Equity: 10000, FreeMargin: 10000);
    private readonly ICopySizingCalculator _calculator = new CopySizingCalculator();

    private CopyVolume Run(RiskSettings risk, double masterVolume = 1, AccountSnapshot? destination = null,
        SymbolSpec? destinationSpec = null, LotBounds? bounds = null, AccountSnapshot? master = null,
        double masterStopDistance = 0)
        => _calculator.Calculate(new CopySizingInput(
            masterVolume,
            master ?? Ten,
            destination ?? Ten,
            Standard,
            destinationSpec ?? Standard,
            risk,
            bounds ?? LotBounds.Unbounded,
            masterStopDistance));

    [Fact]
    public void RiskFromStopLoss_sizes_so_the_destination_risks_the_configured_percent()
        // 10000 balance x 2% = 200 risk; stop 0.0010 x 100000 contract = 100 loss/lot -> 2 lots
        => Run(new RiskSettings(MoneyManagementMode.RiskFromStopLoss, 2), masterStopDistance: 0.0010)
            .Lots.Should().Be(2);

    [Fact]
    public void RiskFromStopLoss_without_a_stop_distance_skips()
        => Run(new RiskSettings(MoneyManagementMode.RiskFromStopLoss, 2), masterStopDistance: 0)
            .Skipped.Should().BeTrue();

    [Fact]
    public void FixedLot_returns_configured_lots()
        => Run(new RiskSettings(MoneyManagementMode.FixedLot, 2)).Lots.Should().Be(2);

    [Fact]
    public void LotMultiplier_scales_master_volume()
        => Run(new RiskSettings(MoneyManagementMode.LotMultiplier, 3), masterVolume: 2).Lots.Should().Be(6);

    [Fact]
    public void NotionalMultiplier_adjusts_for_contract_size()
    {
        var destination = new SymbolSpec(ContractSize: 50000, LotStep: 0.01, MinLot: 0.01, MaxLot: 0);
        Run(new RiskSettings(MoneyManagementMode.NotionalMultiplier, 1), masterVolume: 1, destinationSpec: destination)
            .Lots.Should().Be(2);
    }

    [Fact]
    public void ProportionalBalance_scales_by_account_ratio()
    {
        var destination = new AccountSnapshot(5000, 5000, 5000);
        Run(new RiskSettings(MoneyManagementMode.ProportionalBalance, 1), masterVolume: 1, destination: destination)
            .Lots.Should().Be(0.5);
    }

    [Fact]
    public void FixedRiskPercent_derives_lots_from_balance()
        => Run(new RiskSettings(MoneyManagementMode.FixedRiskPercent, 20),
            destinationSpec: new SymbolSpec(100, 0.01, 0.01, 0)).Lots.Should().Be(20);

    [Fact]
    public void FixedLeverage_is_independent_of_master_volume()
        => Run(new RiskSettings(MoneyManagementMode.FixedLeverage, 0.01), masterVolume: 999,
            destinationSpec: new SymbolSpec(100, 0.01, 0.01, 0)).Lots.Should().Be(1);

    [Fact]
    public void Volume_is_floored_to_lot_step()
        => Run(new RiskSettings(MoneyManagementMode.FixedLot, 0.037)).Lots.Should().Be(0.03);

    [Fact]
    public void Max_lot_bound_caps_volume()
        => Run(new RiskSettings(MoneyManagementMode.FixedLot, 5), bounds: new LotBounds(0, 2, false))
            .Lots.Should().Be(2);

    [Fact]
    public void Below_min_without_force_skips()
        => Run(new RiskSettings(MoneyManagementMode.FixedLot, 0.001),
            destinationSpec: new SymbolSpec(100000, 0.01, 0.5, 0)).Skipped.Should().BeTrue();

    [Fact]
    public void Below_min_with_force_snaps_to_min()
        => Run(new RiskSettings(MoneyManagementMode.FixedLot, 0.001), bounds: new LotBounds(0.5, 0, true))
            .Lots.Should().Be(0.5);

    [Fact]
    public void Zero_master_balance_skips_proportional()
        => Run(new RiskSettings(MoneyManagementMode.ProportionalBalance, 1),
            master: new AccountSnapshot(0, 0, 0)).Skipped.Should().BeTrue();

    [Fact]
    public void ProportionalEquity_scales_by_equity_ratio()
        => Run(new RiskSettings(MoneyManagementMode.ProportionalEquity, 1), masterVolume: 1,
            destination: new AccountSnapshot(5000, 5000, 5000)).Lots.Should().Be(0.5);

    [Fact]
    public void ProportionalFreeMargin_scales_by_free_margin_ratio()
        => Run(new RiskSettings(MoneyManagementMode.ProportionalFreeMargin, 1), masterVolume: 1,
            destination: new AccountSnapshot(20000, 20000, 20000)).Lots.Should().Be(2);

    [Fact]
    public void AutoProportional_scales_by_balance_ratio()
        => Run(new RiskSettings(MoneyManagementMode.AutoProportional, 1), masterVolume: 1,
            destination: new AccountSnapshot(5000, 5000, 5000)).Lots.Should().Be(0.5);

    [Fact]
    public void Proportional_scales_up_for_larger_destination()
        => Run(new RiskSettings(MoneyManagementMode.ProportionalBalance, 1), masterVolume: 1,
            destination: new AccountSnapshot(20000, 20000, 20000)).Lots.Should().Be(2);

    // Leverage mismatch is modelled through proportional/notional sizing: a smaller, lower-capacity
    // destination is scaled down so the copied trade stays inside its margin (the "golden rule").
    [Fact]
    public void Smaller_destination_is_scaled_down_to_fit()
        => Run(new RiskSettings(MoneyManagementMode.ProportionalBalance, 1), masterVolume: 1,
            master: new AccountSnapshot(50000, 50000, 50000),
            destination: new AccountSnapshot(3000, 3000, 3000)).Lots.Should().BeApproximately(0.06, 0.001);

    [Fact]
    public void Symbol_spec_max_lot_caps_volume()
        => Run(new RiskSettings(MoneyManagementMode.FixedLot, 5),
            destinationSpec: new SymbolSpec(100000, 0.01, 0.01, MaxLot: 1.5)).Lots.Should().Be(1.5);

    [Fact]
    public void Tighter_of_bound_and_spec_max_lot_wins()
        => Run(new RiskSettings(MoneyManagementMode.FixedLot, 5), bounds: new LotBounds(0, 3, false),
            destinationSpec: new SymbolSpec(100000, 0.01, 0.01, MaxLot: 2)).Lots.Should().Be(2);

    [Fact]
    public void Symbol_spec_min_lot_skips_when_below_and_not_forced()
        => Run(new RiskSettings(MoneyManagementMode.FixedLot, 0.2),
            destinationSpec: new SymbolSpec(100000, 0.01, MinLot: 0.5, MaxLot: 0)).Skipped.Should().BeTrue();

    [Fact]
    public void Larger_of_bound_and_spec_min_lot_is_enforced()
        => Run(new RiskSettings(MoneyManagementMode.FixedLot, 0.2), bounds: new LotBounds(0.3, 0, true),
            destinationSpec: new SymbolSpec(100000, 0.01, MinLot: 0.5, MaxLot: 0)).Lots.Should().Be(0.5);
}
