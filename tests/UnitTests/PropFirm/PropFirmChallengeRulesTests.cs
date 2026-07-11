using Core;
using Core.Constants;
using Core.Domain;
using Core.PropFirm;
using FluentAssertions;
using Xunit;

namespace UnitTests.PropFirm;

public class PropFirmChallengeRulesTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 07, 10, 10, 00, 00, TimeSpan.Zero);
    private static DateTimeOffset Day(int offset) => Day1.AddDays(offset);

    private static PropFirmChallenge Create(ChallengeRules rules, decimal startingBalance = 100_000m)
        => PropFirmChallenge.Create(UserId.New(), TradingAccountId.New(), "Custom", new Money(startingBalance), rules);

    private static ChallengeRules Rules(
        double profitTarget = 10, double dailyLoss = 5, double maxDrawdown = 10,
        DrawdownMode mode = DrawdownMode.Static, int minDays = 0, bool singleStep = true)
        => new(new Percent(profitTarget), new Percent(dailyLoss), new Percent(maxDrawdown),
            mode, new TradingDayRequirement(minDays), singleStep);

    [Fact]
    public void Balance_basis_daily_loss_ignores_floating_equity_swings()
    {
        var rules = Rules(profitTarget: 50, dailyLoss: 5) with { DailyLossBasis = DailyLossBasis.Balance };
        var challenge = Create(rules);

        // Equity dips 6% but balance (realized) unchanged -> no breach on balance basis.
        challenge.RecordEquity(new EquitySnapshot(new Money(94_000m), new Money(100_000m)), Day1);

        challenge.Status.Should().Be(ChallengeStatus.Active);
    }

    [Fact]
    public void Balance_basis_daily_loss_breaches_on_realized_loss()
    {
        var rules = Rules(profitTarget: 50, dailyLoss: 5) with { DailyLossBasis = DailyLossBasis.Balance };
        var challenge = Create(rules);

        challenge.RecordEquity(new EquitySnapshot(new Money(94_000m), new Money(94_000m)), Day1);

        challenge.Status.Should().Be(ChallengeStatus.Failed);
        challenge.Breach.Should().Be(BreachReason.DailyLoss);
    }

    [Fact]
    public void Trailing_threshold_dollar_trails_peak_then_locks_at_starting_balance()
    {
        var rules = Rules(profitTarget: 50, dailyLoss: 50, mode: DrawdownMode.TrailingThreshold) with
        {
            TrailingThresholdAmount = 5_000m,
            TrailingLockThreshold = 106_000m
        };
        var challenge = Create(rules);

        // Peak 104k, floor = 104k-5k = 99k. Equity 100k -> ok.
        challenge.RecordEquity(new EquitySnapshot(new Money(104_000m), new Money(104_000m)), Day1);
        challenge.RecordEquity(new EquitySnapshot(new Money(100_000m), new Money(100_000m)), Day1);
        challenge.Status.Should().Be(ChallengeStatus.Active);

        // Cross lock threshold (peak 106k) -> floor locks at starting balance 100k.
        challenge.RecordEquity(new EquitySnapshot(new Money(106_000m), new Money(106_000m)), Day1);
        // Drop to 99.5k -> below locked floor 100k -> breach.
        challenge.RecordEquity(new EquitySnapshot(new Money(99_500m), new Money(99_500m)), Day1);

        challenge.Status.Should().Be(ChallengeStatus.Failed);
        challenge.Breach.Should().Be(BreachReason.MaxDrawdown);
    }

    [Fact]
    public void Consistency_rule_blocks_pass_when_one_day_dominates_profit()
    {
        var rules = Rules(profitTarget: 10, dailyLoss: 90, maxDrawdown: 90, minDays: 0) with
        {
            ConsistencyMaxDayProfitSharePercent = 40
        };
        var challenge = Create(rules);

        // Single day makes the whole 10% -> 100% of profit from one day > 40% -> pass blocked.
        challenge.RecordEquity(new EquitySnapshot(new Money(110_000m), new Money(110_000m)), Day1);

        challenge.Status.Should().Be(ChallengeStatus.Active);
    }

    [Fact]
    public void Consistency_rule_allows_pass_when_profit_spread_across_days()
    {
        var rules = Rules(profitTarget: 10, dailyLoss: 90, maxDrawdown: 90, minDays: 0) with
        {
            ConsistencyMaxDayProfitSharePercent = 60
        };
        var challenge = Create(rules);

        challenge.RecordEquity(new EquitySnapshot(new Money(105_000m), new Money(105_000m)), Day1);
        challenge.RecordEquity(new EquitySnapshot(new Money(110_000m), new Money(110_000m)), Day(1));

        challenge.Status.Should().Be(ChallengeStatus.Passed);
    }

    [Fact]
    public void Time_limit_breach_fails_the_challenge()
    {
        var rules = Rules(profitTarget: 50) with { MaxCalendarDays = 30 };
        var challenge = Create(rules);

        challenge.RecordEquity(new Money(101_000m), Day1);
        challenge.RecordEquity(new Money(101_000m), Day(31));

        challenge.Status.Should().Be(ChallengeStatus.Failed);
        challenge.Breach.Should().Be(BreachReason.TimeLimit);
    }

    [Fact]
    public void Inactivity_breach_fails_the_challenge()
    {
        var rules = Rules(profitTarget: 50) with { MaxInactivityDays = 5 };
        var challenge = Create(rules);

        challenge.RecordActivity(new ActivitySnapshot(1, false, false), Day1);
        challenge.RecordEquity(new Money(101_000m), Day(6));

        challenge.Status.Should().Be(ChallengeStatus.Failed);
        challenge.Breach.Should().Be(BreachReason.Inactivity);
    }

    [Fact]
    public void Max_open_positions_breach_fails_the_challenge()
    {
        var rules = Rules(profitTarget: 50) with { MaxOpenPositions = 2 };
        var challenge = Create(rules);

        challenge.RecordActivity(new ActivitySnapshot(3, false, false), Day1);

        challenge.Status.Should().Be(ChallengeStatus.Failed);
        challenge.Breach.Should().Be(BreachReason.MaxExposure);
    }

    [Fact]
    public void Weekend_holding_breach_fails_when_disallowed()
    {
        var rules = Rules(profitTarget: 50) with { AllowWeekendHolding = false };
        var challenge = Create(rules);

        challenge.RecordActivity(new ActivitySnapshot(1, false, true), Day1);

        challenge.Status.Should().Be(ChallengeStatus.Failed);
        challenge.Breach.Should().Be(BreachReason.WeekendHolding);
    }

    [Fact]
    public void News_trading_breach_fails_when_disallowed()
    {
        var rules = Rules(profitTarget: 50) with { AllowNewsTrading = false };
        var challenge = Create(rules);

        challenge.RecordActivity(new ActivitySnapshot(1, true, false), Day1);

        challenge.Status.Should().Be(ChallengeStatus.Failed);
        challenge.Breach.Should().Be(BreachReason.NewsTrading);
    }

    [Fact]
    public void Stop_then_resume_toggles_active_and_stopped()
    {
        var challenge = Create(Rules(profitTarget: 50));

        challenge.Stop();
        challenge.Status.Should().Be(ChallengeStatus.Stopped);
        challenge.DomainEvents.OfType<PropFirmChallengeStopped>().Should().ContainSingle();

        challenge.Resume();
        challenge.Status.Should().Be(ChallengeStatus.Active);
    }

    [Fact]
    public void Stop_on_a_terminal_challenge_throws()
    {
        var challenge = Create(Rules(profitTarget: 10, singleStep: true));
        challenge.RecordEquity(new Money(110_000m), Day1);

        var act = () => challenge.Stop();
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.PropFirmChallengeTransitionInvalid);
    }

    [Fact]
    public void Lease_is_held_only_by_the_owning_node_before_expiry()
    {
        var challenge = Create(Rules(profitTarget: 50));
        var node = new NodeIdentity("node-a");
        var other = new NodeIdentity("node-b");

        challenge.ClaimBy(node, Day1.AddMinutes(5));

        challenge.IsLeaseHeldBy(node, Day1).Should().BeTrue();
        challenge.IsLeaseHeldBy(other, Day1).Should().BeFalse();
        challenge.IsLeaseHeldBy(node, Day1.AddMinutes(5)).Should().BeFalse("the lease is expired exactly at its end");
    }

    [Fact]
    public void Passing_releases_the_node_lease()
    {
        var challenge = Create(Rules(profitTarget: 10, singleStep: true));
        challenge.ClaimBy(new NodeIdentity("node-a"), Day1.AddMinutes(5));

        challenge.RecordEquity(new Money(110_000m), Day1);

        challenge.AssignedNode.Should().BeNull();
        challenge.Status.Should().Be(ChallengeStatus.Passed);
    }

    [Fact]
    public void Drawdown_warning_raised_once_when_threshold_crossed()
    {
        var challenge = Create(Rules(profitTarget: 50, dailyLoss: 50, maxDrawdown: 10));
        challenge.SetDrawdownWarnThreshold(80);

        // 8% loss of 100k = 8000; max drawdown 10% = 10000; used = 80%.
        challenge.RecordEquity(new Money(92_000m), Day1);
        challenge.RecordEquity(new Money(91_500m), Day1);

        challenge.DomainEvents.OfType<PropFirmDrawdownWarning>().Should().ContainSingle();
    }
}
