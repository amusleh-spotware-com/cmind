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
}
