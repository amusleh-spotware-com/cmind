using Core.PropFirm;
using FluentAssertions;
using Xunit;

namespace UnitTests.PropFirm;

// Every preset builds valid rules tagged with its own kind, with the shape-defining fields set.
// (WS-1 Core backfill.)
public class ChallengeTemplatesTests
{
    [Theory]
    [InlineData(ChallengeKind.OnePhase)]
    [InlineData(ChallengeKind.TwoPhase)]
    [InlineData(ChallengeKind.ThreePhase)]
    [InlineData(ChallengeKind.InstantFunding)]
    [InlineData(ChallengeKind.Custom)]
    public void For_tags_the_rules_with_the_requested_kind(ChallengeKind kind)
    {
        ChallengeTemplates.For(kind).Kind.Should().Be(kind);
    }

    [Fact]
    public void One_phase_is_single_step_with_static_drawdown()
    {
        var rules = ChallengeTemplates.For(ChallengeKind.OnePhase);
        rules.SingleStep.Should().BeTrue();
        rules.DrawdownMode.Should().Be(DrawdownMode.Static);
        rules.MinTradingDays.Should().Be(new TradingDayRequirement(0));
    }

    [Fact]
    public void Two_phase_is_multi_step_and_needs_trading_days()
    {
        var rules = ChallengeTemplates.For(ChallengeKind.TwoPhase);
        rules.SingleStep.Should().BeFalse();
        rules.MinTradingDays.Should().Be(new TradingDayRequirement(3));
    }

    [Fact]
    public void Instant_funding_uses_trailing_drawdown()
    {
        var rules = ChallengeTemplates.For(ChallengeKind.InstantFunding);
        rules.DrawdownMode.Should().Be(DrawdownMode.Trailing);
        rules.SingleStep.Should().BeTrue();
    }

    [Fact]
    public void All_lists_every_kind()
    {
        ChallengeTemplates.All.Should().BeEquivalentTo(
        [
            ChallengeKind.OnePhase, ChallengeKind.TwoPhase, ChallengeKind.ThreePhase,
            ChallengeKind.InstantFunding, ChallengeKind.Custom
        ]);
    }
}
