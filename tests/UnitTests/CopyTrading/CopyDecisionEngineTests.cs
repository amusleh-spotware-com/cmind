using Core;
using Core.Domain;
using CopyEngine;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class CopyDecisionEngineTests
{
    private static readonly SymbolSpec Spec = new(100000, 0.01, 0.01, 0);
    private static readonly AccountSnapshot Account = new(10000, 10000, 10000);
    private readonly CopyDecisionEngine _engine = new(new CopySizingCalculator());

    private static CopyDestination Destination(Action<CopyDestination>? configure = null)
    {
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        var destination = profile.AddDestination(TradingAccountId.New(),
            new RiskSettings(MoneyManagementMode.LotMultiplier, 1));
        configure?.Invoke(destination);
        return destination;
    }

    private static OpenDecisionContext Context(bool isLong = true, double openPrice = 1.1000,
        double destinationPrice = 1.1000, TimeSpan? age = null) =>
        new(new SourcePosition(1, "EURUSD", isLong, 1, openPrice, null, null),
            Account, Account, Spec, Spec, destinationPrice, 0.0001, age ?? TimeSpan.Zero);

    [Fact]
    public void Open_within_all_limits_produces_open_action()
    {
        var action = _engine.DecideOpen(Destination(), Context());
        action.Kind.Should().Be(CopyActionKind.Open);
        action.Lots.Should().Be(1);
    }

    [Fact]
    public void Slippage_beyond_limit_is_skipped()
    {
        var destination = Destination(d => d.ConfigureSlippage(new SlippagePips(5)));
        var action = _engine.DecideOpen(destination, Context(openPrice: 1.1000, destinationPrice: 1.1010));
        action.Kind.Should().Be(CopyActionKind.Skip);
        action.SkipReason.Should().Be("slippage");
    }

    [Fact]
    public void Stale_event_beyond_max_delay_is_skipped()
    {
        var destination = Destination(d => d.ConfigureMaxDelay(MaxCopyDelay.Seconds(2)));
        var action = _engine.DecideOpen(destination, Context(age: TimeSpan.FromSeconds(5)));
        action.SkipReason.Should().Be("max_delay");
    }

    [Fact]
    public void Direction_filter_blocks_opposite_side()
    {
        var destination = Destination(d => d.SetDirection(CopyDirectionFilter.LongOnly));
        var action = _engine.DecideOpen(destination, Context(isLong: false));
        action.SkipReason.Should().Be("direction");
    }

    [Fact]
    public void Reverse_flips_effective_direction_for_filter()
    {
        var destination = Destination(d =>
        {
            d.SetReverse(true);
            d.SetDirection(CopyDirectionFilter.LongOnly);
        });
        // source short, reversed => effective long => passes LongOnly
        var action = _engine.DecideOpen(destination, Context(isLong: false));
        action.Kind.Should().Be(CopyActionKind.Open);
        action.Reversed.Should().BeTrue();
    }

    [Fact]
    public void Whitelist_blocks_unlisted_symbol()
    {
        var destination = Destination(d => d.SetSymbolFilter(SymbolFilterMode.Whitelist, [new Symbol("GBPUSD")]));
        _engine.DecideOpen(destination, Context()).SkipReason.Should().Be("symbol_filter");
    }

    [Fact]
    public void Blacklist_blocks_listed_symbol()
    {
        var destination = Destination(d => d.SetSymbolFilter(SymbolFilterMode.Blacklist, [new Symbol("EURUSD")]));
        _engine.DecideOpen(destination, Context()).SkipReason.Should().Be("symbol_filter");
    }

    [Fact]
    public void Whitelist_allows_listed_symbol()
    {
        var destination = Destination(d => d.SetSymbolFilter(SymbolFilterMode.Whitelist, [new Symbol("EURUSD")]));
        _engine.DecideOpen(destination, Context()).Kind.Should().Be(CopyActionKind.Open);
    }

    [Fact]
    public void ShortOnly_blocks_long_source()
    {
        var destination = Destination(d => d.SetDirection(CopyDirectionFilter.ShortOnly));
        _engine.DecideOpen(destination, Context(isLong: true)).SkipReason.Should().Be("direction");
    }

    [Fact]
    public void ShortOnly_allows_short_source()
    {
        var destination = Destination(d => d.SetDirection(CopyDirectionFilter.ShortOnly));
        _engine.DecideOpen(destination, Context(isLong: false)).Kind.Should().Be(CopyActionKind.Open);
    }

    [Fact]
    public void Slippage_exactly_at_limit_is_allowed()
    {
        var destination = Destination(d => d.ConfigureSlippage(new SlippagePips(5)));
        // 1.1000 -> 1.1005 at 0.0001 pip size == exactly 5 pips, which is within the limit.
        _engine.DecideOpen(destination, Context(openPrice: 1.1000, destinationPrice: 1.1005))
            .Kind.Should().Be(CopyActionKind.Open);
    }

    [Fact]
    public void Zero_sized_destination_is_skipped()
    {
        // Bound the min lot above the copied size with no force -> the sizer skips -> size_zero.
        var destination = Destination(d => d.ConfigureBounds(new LotBounds(100, 0, false)));
        _engine.DecideOpen(destination, Context()).SkipReason.Should().Be("size_zero");
    }

    [Fact]
    public void Lot_sanity_absolute_cap_blocks_an_oversize_copy()
    {
        // Master 1 lot, 1x multiplier -> copy 1 lot, but the absolute ceiling is 0.5.
        var destination = Destination(d => d.ConfigureLotSanity(new LotSanityCeiling(0.5, 0)));
        _engine.DecideOpen(destination, Context()).SkipReason.Should().Be("lot_sanity");
    }

    [Fact]
    public void Lot_sanity_master_multiple_blocks_a_runaway_multiplier()
    {
        // 5x multiplier on a 1-lot master -> copy 5 lots, which exceeds 3x the master's own size.
        var destination = Destination(d =>
        {
            d.ConfigureRisk(new RiskSettings(MoneyManagementMode.LotMultiplier, 5));
            d.ConfigureLotSanity(new LotSanityCeiling(0, 3));
        });
        _engine.DecideOpen(destination, Context()).SkipReason.Should().Be("lot_sanity");
    }

    [Fact]
    public void Lot_sanity_within_the_ceiling_allows_the_copy()
    {
        var destination = Destination(d => d.ConfigureLotSanity(new LotSanityCeiling(2, 3)));
        _engine.DecideOpen(destination, Context()).Kind.Should().Be(CopyActionKind.Open);
    }

    [Fact]
    public void Risk_from_stop_loss_without_a_master_stop_is_skipped()
    {
        // Context()'s source carries no stop-loss, so risk-from-stop has nothing to size against.
        var destination = Destination(d => d.ConfigureRisk(new RiskSettings(MoneyManagementMode.RiskFromStopLoss, 2)));
        _engine.DecideOpen(destination, Context()).SkipReason.Should().Be("no_stop_loss");
    }

    [Fact]
    public void Risk_from_stop_loss_sizes_off_the_master_stop_distance()
    {
        var destination = Destination(d => d.ConfigureRisk(new RiskSettings(MoneyManagementMode.RiskFromStopLoss, 2)));
        var context = new OpenDecisionContext(
            new SourcePosition(1, "EURUSD", IsLong: true, VolumeLots: 1, OpenPrice: 1.1000, StopLoss: 1.0990, TakeProfit: null),
            Account, Account, Spec, Spec, DestinationPrice: 1.1000, DestinationPipSize: 0.0001, EventAge: TimeSpan.Zero);

        var action = _engine.DecideOpen(destination, context);

        action.Kind.Should().Be(CopyActionKind.Open);
        action.Lots.Should().BeApproximately(2, 0.02,
            "10000 x 2% = 200 risk over a ~0.0010 stop x 100000 contract = ~100/lot -> ~2 lots (floored to lot step)");
    }

    [Fact]
    public void PositionsToOpen_excludes_already_mapped_sources()
    {
        var map = new Dictionary<long, long> { [10] = 100 };
        _engine.PositionsToOpen([10, 11, 12], map).Should().BeEquivalentTo([11L, 12L]);
    }

    [Fact]
    public void DestinationPositionsToClose_returns_orphaned_copies()
    {
        var map = new Dictionary<long, long> { [10] = 100, [11] = 101 };
        _engine.DestinationPositionsToClose([10], map).Should().BeEquivalentTo([101L]);
    }
}
