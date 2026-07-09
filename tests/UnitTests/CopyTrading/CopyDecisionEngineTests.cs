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
