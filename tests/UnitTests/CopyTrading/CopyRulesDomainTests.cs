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
