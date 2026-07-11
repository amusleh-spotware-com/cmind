using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

// Invariants for the per-destination copy rules (order-type filter, expiry, master-slippage) and the
// single-valid-token-per-cID version marker. Asserts the aggregate guards, not getters/setters.
public sealed class CopyRulesDomainTests
{
    private static CopyDestination Destination()
        => CopyProfile.Create(UserId.New(), "p", TradingAccountId.New())
            .AddDestination(TradingAccountId.New(), RiskSettings.Default);

    [Fact]
    public void New_destination_copies_all_order_types_and_mirrors_expiry_and_slippage_by_default()
    {
        var destination = Destination();

        destination.CopyOrderTypes.Should().Be(CopyOrderTypes.All);
        destination.CopyPendingExpiry.Should().BeTrue();
        destination.CopyMasterSlippage.Should().BeTrue();
        destination.IsOrderTypeAllowed(CopyOrderTypes.Market).Should().BeTrue();
        destination.IsOrderTypeAllowed(CopyOrderTypes.StopLimit).Should().BeTrue();
    }

    [Fact]
    public void Order_type_filter_allows_only_the_selected_types()
    {
        var destination = Destination();

        destination.SetOrderTypeFilter(CopyOrderTypes.Limit | CopyOrderTypes.Stop);

        destination.IsOrderTypeAllowed(CopyOrderTypes.Limit).Should().BeTrue();
        destination.IsOrderTypeAllowed(CopyOrderTypes.Stop).Should().BeTrue();
        destination.IsOrderTypeAllowed(CopyOrderTypes.Market).Should().BeFalse();
        destination.IsOrderTypeAllowed(CopyOrderTypes.MarketRange).Should().BeFalse();
    }

    [Fact]
    public void Order_type_filter_rejects_an_empty_selection()
    {
        var destination = Destination();

        var act = () => destination.SetOrderTypeFilter(CopyOrderTypes.None);

        act.Should().Throw<DomainException>().Which.Message.Should().Be(DomainErrors.CopyOrderTypesInvalid);
    }

    [Fact]
    public void Lot_sanity_ceiling_rejects_a_negative_bound()
    {
        var act = () => new LotSanityCeiling(-1, 0);

        act.Should().Throw<DomainException>().Which.Message.Should().Be(DomainErrors.CopyLotSanityInvalid);
    }

    [Fact]
    public void Lot_sanity_disabled_never_breaches()
    {
        LotSanityCeiling.Disabled.IsBreached(1000, 1).Should().BeFalse("a disabled ceiling blocks nothing");
    }

    [Theory]
    [InlineData(540, 1020, 600, true)]   // 09:00–17:00, now 10:00 -> open
    [InlineData(540, 1020, 300, false)]  // now 05:00 -> closed
    [InlineData(1320, 360, 1380, true)]  // 22:00–06:00 wrap, now 23:00 -> open
    [InlineData(1320, 360, 720, false)]  // wrap window, now 12:00 -> closed
    [InlineData(0, 0, 720, true)]        // all-day (disabled) -> always open
    public void Trading_window_open_state(int start, int end, int nowMinute, bool expected)
        => new TradingWindow(start, end).IsOpenAt(nowMinute).Should().Be(expected);

    [Fact]
    public void Trading_window_rejects_an_out_of_range_minute()
    {
        var act = () => new TradingWindow(-1, 100);
        act.Should().Throw<DomainException>().Which.Message.Should().Be(DomainErrors.CopyTradingWindowInvalid);
    }

    [Theory]
    [InlineData(AccountProtectionMode.SellOut, 5000, null, 4000, true)]   // equity below stop -> triggered
    [InlineData(AccountProtectionMode.SellOut, 5000, null, 6000, false)]  // above stop -> safe
    [InlineData(AccountProtectionMode.CloseOnly, 5000, 12000.0, 12500, true)] // above take ceiling -> triggered
    [InlineData(AccountProtectionMode.Off, 5000, null, 1000, false)]     // off never triggers
    public void Account_protection_trigger_state(AccountProtectionMode mode, double stop, double? take, double equity, bool expected)
        => new AccountProtectionPolicy(mode, stop, take).IsTriggered(equity).Should().Be(expected);

    [Fact]
    public void Account_protection_sell_out_requires_a_stop_equity()
    {
        var act = () => new AccountProtectionPolicy(AccountProtectionMode.SellOut, 0, null);
        act.Should().Throw<DomainException>().Which.Message.Should().Be(DomainErrors.CopyAccountProtectionInvalid);
    }

    [Fact]
    public void Account_protection_rejects_a_take_below_the_stop()
    {
        var act = () => new AccountProtectionPolicy(AccountProtectionMode.CloseOnly, 5000, 4000);
        act.Should().Throw<DomainException>().Which.Message.Should().Be(DomainErrors.CopyAccountProtectionInvalid);
    }

    [Fact]
    public void Prop_rule_guard_evaluates_daily_loss_and_trailing_drawdown()
    {
        var guard = new PropRuleGuard(dailyLossCap: 1500, trailingDrawdown: 800);

        guard.IsEnabled.Should().BeTrue();
        guard.DailyLossBreached(dayStartEquity: 10000, equity: 8400).Should().BeTrue("loss 1600 >= 1500 cap");
        guard.DailyLossBreached(dayStartEquity: 10000, equity: 9000).Should().BeFalse("loss 1000 < cap");
        guard.TrailingDrawdownBreached(peakEquity: 11000, equity: 10100).Should().BeTrue("drawdown 900 >= 800");
        guard.TrailingDrawdownBreached(peakEquity: 11000, equity: 10500).Should().BeFalse("drawdown 500 < limit");
        PropRuleGuard.Disabled.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Prop_rule_guard_rejects_a_negative_limit()
    {
        var act = () => new PropRuleGuard(-1, 0);
        act.Should().Throw<DomainException>().Which.Message.Should().Be(DomainErrors.CopyPropRuleInvalid);
    }

    [Fact]
    public void Consistency_threshold_rejects_a_negative_percent()
    {
        var destination = Destination();
        var act = () => destination.SetConsistencyThreshold(-1);
        act.Should().Throw<DomainException>().Which.Message.Should().Be(DomainErrors.CopyRiskParameterInvalid);
    }

    [Fact]
    public void Config_locked_destination_cannot_be_removed_until_the_lock_expires()
    {
        var now = new DateTimeOffset(2026, 07, 11, 12, 00, 00, TimeSpan.Zero);
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        var destination = profile.AddDestination(TradingAccountId.New(), RiskSettings.Default);
        destination.LockConfig(now.AddMinutes(30));

        var locked = () => profile.RemoveDestination(destination.Id, now);
        locked.Should().Throw<DomainException>().Which.Message.Should().Be(DomainErrors.CopyDestinationConfigLocked);

        profile.RemoveDestination(destination.Id, now.AddMinutes(31)); // lock expired -> removal allowed
        profile.Destinations.Should().BeEmpty();
    }

    [Fact]
    public void Source_label_filter_matches_exactly_and_allows_all_when_unset()
    {
        var destination = Destination();
        destination.IsSourceLabelAllowed("anything").Should().BeTrue("no filter copies every master trade");

        destination.SetSourceLabelFilter("botA");
        destination.IsSourceLabelAllowed("botA").Should().BeTrue();
        destination.IsSourceLabelAllowed("botB").Should().BeFalse();
        destination.IsSourceLabelAllowed(null).Should().BeFalse("an unlabelled master trade is filtered out");

        destination.SetSourceLabelFilter("   ");
        destination.IsSourceLabelAllowed("botB").Should().BeTrue("blank filter clears the restriction");
    }

    [Fact]
    public void Expiry_and_slippage_copying_can_be_disabled()
    {
        var destination = Destination();

        destination.SetExpiryCopying(false);
        destination.SetSlippageCopying(false);

        destination.CopyPendingExpiry.Should().BeFalse();
        destination.CopyMasterSlippage.Should().BeFalse();
    }

    [Fact]
    public void Lease_is_held_only_by_the_claiming_node_until_it_expires()
    {
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        profile.Start();
        var node = new NodeIdentity("node-a");
        var now = TestClock.Now;

        profile.ClaimBy(node, now.AddMinutes(1));

        profile.IsLeaseHeldBy(node, now).Should().BeTrue();
        profile.IsLeaseHeldBy(new NodeIdentity("node-b"), now).Should().BeFalse();
        profile.IsLeaseHeldBy(node, now.AddMinutes(2)).Should().BeFalse();
    }

    [Fact]
    public void Refreshing_the_authorization_bumps_the_token_version()
    {
        var authorization = OpenApiAuthorization.Create(UserId.New(), OpenApiApplicationId.New(),
            new CtidUserId(42), isLive: false, [1], [2], TestClock.Now.AddHours(1), OpenApiScope.Trade);
        var initial = authorization.TokenVersion;

        authorization.Refresh([3], [4], TestClock.Now.AddHours(2), TestClock.Now);

        authorization.TokenVersion.Should().Be(initial + 1);
    }
}
