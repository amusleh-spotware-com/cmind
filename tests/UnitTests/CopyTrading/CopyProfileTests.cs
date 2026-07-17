using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class CopyProfileTests
{
    private static readonly RiskSettings Mirror = new(MoneyManagementMode.LotMultiplier, 1);

    private static CopyProfile NewProfile(out TradingAccountId source)
    {
        source = TradingAccountId.New();
        return CopyProfile.Create(UserId.New(), "profile", source);
    }

    [Fact]
    public void Create_starts_in_draft()
        => NewProfile(out _).Status.Should().Be(CopyProfileStatus.Draft);

    [Fact]
    public void AddDestination_rejects_source_as_destination()
    {
        var profile = NewProfile(out var source);
        var act = () => profile.AddDestination(source, Mirror);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CopySourceEqualsDestination);
    }

    [Fact]
    public void AddDestination_rejects_duplicate()
    {
        var profile = NewProfile(out _);
        var dest = TradingAccountId.New();
        profile.AddDestination(dest, Mirror);
        var act = () => profile.AddDestination(dest, Mirror);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CopyDestinationDuplicate);
    }

    [Fact]
    public void ChangeSource_updates_the_source_account()
    {
        var profile = NewProfile(out _);
        var newSource = TradingAccountId.New();
        profile.ChangeSource(newSource);
        profile.SourceAccountId.Should().Be(newSource);
    }

    [Fact]
    public void ChangeSource_rejects_an_existing_destination()
    {
        var profile = NewProfile(out _);
        var dest = TradingAccountId.New();
        profile.AddDestination(dest, Mirror);
        var act = () => profile.ChangeSource(dest);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CopySourceEqualsDestination);
    }

    [Fact]
    public void Start_transitions_running_and_raises_event()
    {
        var profile = NewProfile(out _);
        profile.Start();
        profile.Status.Should().Be(CopyProfileStatus.Running);
        profile.DomainEvents.OfType<CopyProfileStarted>().Should().ContainSingle();
    }

    [Fact]
    public void Pause_only_valid_from_running()
    {
        var profile = NewProfile(out _);
        var act = () => profile.Pause();
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CopyProfileTransitionInvalid);
    }

    [Fact]
    public void Stop_then_start_is_allowed()
    {
        var profile = NewProfile(out _);
        profile.Start();
        profile.Stop();
        profile.Start();
        profile.Status.Should().Be(CopyProfileStatus.Running);
    }

    [Theory]
    [InlineData(MoneyManagementMode.FixedLot, 0)]
    [InlineData(MoneyManagementMode.LotMultiplier, -1)]
    [InlineData(MoneyManagementMode.FixedRiskPercent, 150)]
    public void RiskSettings_rejects_invalid_parameter(MoneyManagementMode mode, double parameter)
    {
        var act = () => _ = new RiskSettings(mode, parameter);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void LotBounds_rejects_max_below_min()
    {
        var act = () => _ = new LotBounds(2, 1, false);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CopyLotBoundsInvalid);
    }

    [Fact]
    public void Destination_resolves_symbol_mapping()
    {
        var profile = NewProfile(out _);
        var destination = profile.AddDestination(TradingAccountId.New(), Mirror);
        destination.SetSymbolMap([new SymbolMapEntry(new Symbol("BTCUSD"), new Symbol("BTCUSD.x"))]);

        destination.ResolveDestinationSymbol("BTCUSD").Should().Be("BTCUSD.X");
        destination.ResolveDestinationSymbol("EURUSD").Should().Be("EURUSD");
    }

    [Fact]
    public void Destination_defaults_mirror_partial_close_and_pending_orders_on()
    {
        var destination = NewProfile(out _).AddDestination(TradingAccountId.New(), Mirror);
        destination.MirrorPartialClose.Should().BeTrue();
        // Pending orders copy by default, like market orders (they were the confusing odd-one-out at off).
        destination.CopyPendingOrders.Should().BeTrue();
        destination.MirrorScaleIn.Should().BeFalse();
        destination.CopyTrailingStop.Should().BeFalse();
    }

    [Fact]
    public void Destination_flag_intention_methods_mutate_state()
    {
        var destination = NewProfile(out _).AddDestination(TradingAccountId.New(), Mirror);
        destination.SetPartialCloseMirroring(false, true);
        destination.SetPendingOrderCopying(true);
        destination.SetTrailingStopCopying(true);

        destination.MirrorPartialClose.Should().BeFalse();
        destination.MirrorScaleIn.Should().BeTrue();
        destination.CopyPendingOrders.Should().BeTrue();
        destination.CopyTrailingStop.Should().BeTrue();
    }

    [Fact]
    public void NodeIdentity_rejects_blank()
    {
        var act = () => _ = new NodeIdentity("  ");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CopyNodeIdentityInvalid);
    }

    [Fact]
    public void AssignToNode_makes_profile_hosted_by_only_that_node()
    {
        var profile = NewProfile(out _);
        profile.AssignToNode(new NodeIdentity("node-a"));

        profile.AssignedNode.Should().Be("node-a");
        profile.IsHostedBy(new NodeIdentity("node-a")).Should().BeTrue();
        profile.IsHostedBy(new NodeIdentity("node-b")).Should().BeFalse();
    }

    [Fact]
    public void Stopping_a_profile_releases_its_node_assignment()
    {
        var profile = NewProfile(out _);
        profile.Start();
        profile.AssignToNode(new NodeIdentity("node-a"));
        profile.Stop();

        profile.AssignedNode.Should().BeNull("a stopped profile can be re-claimed by any node");
        profile.IsHostedBy(new NodeIdentity("node-a")).Should().BeFalse();
    }
}
