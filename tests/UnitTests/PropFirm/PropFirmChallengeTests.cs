using Core;
using Core.Constants;
using Core.Domain;
using Core.PropFirm;
using FluentAssertions;
using Xunit;

namespace UnitTests.PropFirm;

public class PropFirmChallengeTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 07, 10, 10, 00, 00, TimeSpan.Zero);
    private static DateTimeOffset Day(int offset) => Day1.AddDays(offset);

    private static PropFirmChallenge Create(
        double profitTarget = 10, double dailyLoss = 5, double maxDrawdown = 10,
        DrawdownMode mode = DrawdownMode.Static, int minDays = 0, bool singleStep = true,
        decimal startingBalance = 100_000m)
        => PropFirmChallenge.Create(UserId.New(), TradingAccountId.New(), "FTMO 100k",
            new Money(startingBalance),
            new ChallengeRules(new Percent(profitTarget), new Percent(dailyLoss), new Percent(maxDrawdown),
                mode, new TradingDayRequirement(minDays), singleStep));

    [Fact]
    public void Create_starts_in_evaluation_active_and_raises_event()
    {
        var challenge = Create();

        challenge.Phase.Should().Be(ChallengePhase.Evaluation);
        challenge.Status.Should().Be(ChallengeStatus.Active);
        challenge.CurrentEquity.Should().Be(100_000m);
        challenge.DomainEvents.OfType<PropFirmChallengeStarted>().Should().ContainSingle();
    }

    [Fact]
    public void Create_rejects_non_positive_starting_balance()
    {
        var act = () => Create(startingBalance: 0m);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.PropFirmStartingBalanceInvalid);
    }

    [Fact]
    public void Single_step_passes_to_funded_when_target_and_days_met()
    {
        var challenge = Create(profitTarget: 10, minDays: 0, singleStep: true);

        challenge.RecordEquity(new Money(110_000m), Day1);

        challenge.Phase.Should().Be(ChallengePhase.Funded);
        challenge.Status.Should().Be(ChallengeStatus.Passed);
        challenge.DomainEvents.OfType<PropFirmChallengePassed>().Should().ContainSingle();
    }

    [Fact]
    public void Two_step_advances_to_verification_then_funded()
    {
        var challenge = Create(profitTarget: 10, minDays: 0, singleStep: false);

        challenge.RecordEquity(new Money(110_000m), Day1);
        challenge.Phase.Should().Be(ChallengePhase.Verification);
        challenge.Status.Should().Be(ChallengeStatus.Active);
        challenge.CurrentEquity.Should().Be(100_000m, "phase advance resets the equity baseline");

        challenge.RecordEquity(new Money(110_000m), Day(1));
        challenge.Phase.Should().Be(ChallengePhase.Funded);
        challenge.Status.Should().Be(ChallengeStatus.Passed);
    }

    [Fact]
    public void Profit_target_hit_but_min_days_not_met_stays_active()
    {
        var challenge = Create(profitTarget: 10, minDays: 3, singleStep: true);

        challenge.RecordEquity(new Money(110_000m), Day1);

        challenge.Status.Should().Be(ChallengeStatus.Active);
        challenge.TradingDaysCount.Should().Be(1);
    }

    [Fact]
    public void Min_days_met_over_distinct_days_then_passes()
    {
        var challenge = Create(profitTarget: 10, minDays: 3, singleStep: true);

        challenge.RecordEquity(new Money(110_000m), Day1);
        challenge.RecordEquity(new Money(110_000m), Day(1));
        challenge.Status.Should().Be(ChallengeStatus.Active);
        challenge.RecordEquity(new Money(110_000m), Day(2));

        challenge.TradingDaysCount.Should().Be(3);
        challenge.Status.Should().Be(ChallengeStatus.Passed);
    }

    [Fact]
    public void Daily_loss_breach_fails_the_challenge()
    {
        var challenge = Create(dailyLoss: 5);

        challenge.RecordEquity(new Money(100_000m), Day1);
        challenge.RecordEquity(new Money(94_000m), Day1);

        challenge.Status.Should().Be(ChallengeStatus.Failed);
        challenge.Breach.Should().Be(BreachReason.DailyLoss);
        challenge.DomainEvents.OfType<PropFirmChallengeBreached>().Should().ContainSingle();
    }

    [Fact]
    public void Static_drawdown_breach_fails_the_challenge()
    {
        var challenge = Create(dailyLoss: 50, maxDrawdown: 10, mode: DrawdownMode.Static);

        challenge.RecordEquity(new Money(89_000m), Day1);

        challenge.Status.Should().Be(ChallengeStatus.Failed);
        challenge.Breach.Should().Be(BreachReason.MaxDrawdown);
    }

    [Fact]
    public void Trailing_drawdown_breach_measures_from_peak()
    {
        var challenge = Create(profitTarget: 50, dailyLoss: 50, maxDrawdown: 10, mode: DrawdownMode.Trailing);

        challenge.RecordEquity(new Money(120_000m), Day1);
        challenge.PeakEquity.Should().Be(120_000m);
        challenge.RecordEquity(new Money(107_000m), Day1);

        challenge.Status.Should().Be(ChallengeStatus.Failed);
        challenge.Breach.Should().Be(BreachReason.MaxDrawdown);
    }

    [Fact]
    public void Recording_an_out_of_order_equity_snapshot_throws()
    {
        var challenge = Create(profitTarget: 50);
        challenge.RecordEquity(new Money(100_000m), Day(1));

        var act = () => challenge.RecordEquity(new Money(100_000m), Day1);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.PropFirmEquityOutOfOrder);
    }

    [Fact]
    public void Recording_equity_on_a_terminal_challenge_throws()
    {
        var challenge = Create(profitTarget: 10, singleStep: true);
        challenge.RecordEquity(new Money(110_000m), Day1);

        var act = () => challenge.RecordEquity(new Money(120_000m), Day(1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.PropFirmChallengeNotActive);
    }
}
