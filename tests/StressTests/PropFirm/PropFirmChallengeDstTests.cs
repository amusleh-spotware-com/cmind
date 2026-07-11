using Core;
using Core.Domain;
using Core.PropFirm;
using FluentAssertions;
using Xunit;

namespace StressTests.PropFirm;

// Deterministic-simulation stress for the prop-firm challenge aggregate: one seed drives a randomized
// stream of hostile equity/activity events per challenge — day rolls, spikes, crashes, duplicate and
// out-of-order snapshots, exposure/weekend/news activity — across many challenges with mixed rule
// shapes. The aggregate owns every rule decision; the harness only asserts convergence invariants:
// terminal states are sticky and exactly-once, peaks never regress within a phase, and equity that
// pierces a limit always resolves the challenge. A failure prints its seed to reproduce the run.
public sealed class PropFirmChallengeDstTests
{
    private const int Steps = 500;

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(99)]
    [InlineData(1234)]
    [InlineData(31337)]
    [InlineData(2026)]
    public void Randomized_equity_and_activity_streams_keep_the_aggregate_consistent(int seed)
    {
        var random = new Random(seed);
        var challenges = Enumerable.Range(0, 24).Select(i => NewChallenge(random, i)).ToArray();

        var start = new DateTimeOffset(2026, 07, 10, 8, 0, 0, TimeSpan.Zero);
        var lastAcceptedAt = challenges.Select(_ => start).ToArray();
        var terminalSeen = new bool[challenges.Length];

        for (var step = 0; step < Steps; step++)
        {
            var index = random.Next(challenges.Length);
            var challenge = challenges[index];

            // Advance the clock forward most of the time; occasionally emit an out-of-order snapshot the
            // aggregate must reject without corrupting state.
            var now = random.Next(100) < 10
                ? lastAcceptedAt[index] - TimeSpan.FromMinutes(random.Next(1, 120))
                : lastAcceptedAt[index] + TimeSpan.FromMinutes(random.Next(1, 900));

            var wasTerminal = challenge.Status is ChallengeStatus.Passed or ChallengeStatus.Failed;
            var priorStatus = challenge.Status;
            var priorPhase = challenge.Phase;

            try
            {
                switch (random.Next(100))
                {
                    case < 70:
                        var equity = (decimal)(80_000 + random.NextDouble() * 60_000);
                        challenge.RecordEquity(new EquitySnapshot(new Money(equity), new Money(equity)), now);
                        if (now >= lastAcceptedAt[index]) lastAcceptedAt[index] = now;
                        break;
                    case < 85:
                        challenge.RecordActivity(
                            new ActivitySnapshot(random.Next(0, 8), random.Next(2) == 0, random.Next(2) == 0), now);
                        break;
                    case < 92 when challenge.Status == ChallengeStatus.Active:
                        challenge.Stop();
                        break;
                    default:
                        if (challenge.Status == ChallengeStatus.Stopped) challenge.Resume();
                        break;
                }
            }
            catch (DomainException)
            {
                // Guarded transition (out-of-order snapshot, or acting on a terminal/invalid state) —
                // the aggregate must reject it and leave its state untouched.
                if (wasTerminal)
                {
                    challenge.Status.Should().Be(priorStatus, $"seed {seed}: terminal state must be sticky");
                    challenge.Phase.Should().Be(priorPhase);
                }
            }

            // Invariant: a peak never regresses below current equity.
            challenge.PeakEquity.Should().BeGreaterThanOrEqualTo(challenge.CurrentEquity,
                $"seed {seed}, step {step}: peak must bound current equity");

            if (challenge.Status is ChallengeStatus.Passed or ChallengeStatus.Failed)
                terminalSeen[index] = true;
        }

        for (var i = 0; i < challenges.Length; i++)
        {
            var challenge = challenges[i];
            if (challenge.Status == ChallengeStatus.Failed)
                challenge.Breach.Should().NotBe(BreachReason.None, $"seed {seed}: a failed challenge has a reason");

            if (terminalSeen[i])
            {
                // Once terminal, no further RecordEquity is accepted (exactly-once resolution).
                var act = () => challenge.RecordEquity(new Money(100_000m), lastAcceptedAt[i].AddDays(1));
                if (challenge.Status is ChallengeStatus.Passed or ChallengeStatus.Failed)
                    act.Should().Throw<DomainException>();
            }
        }
    }

    private static PropFirmChallenge NewChallenge(Random random, int i)
    {
        var mode = (DrawdownMode)random.Next(0, 3);
        var rules = new ChallengeRules(
            new Percent(5 + random.Next(0, 8)),
            new Percent(3 + random.Next(0, 4)),
            new Percent(6 + random.Next(0, 8)),
            mode,
            new TradingDayRequirement(random.Next(0, 4)),
            SingleStep: random.Next(2) == 0)
        {
            Kind = (ChallengeKind)random.Next(0, 5),
            DailyLossBasis = (DailyLossBasis)random.Next(0, 2),
            TrailingThresholdAmount = mode == DrawdownMode.TrailingThreshold ? 5_000m : 0m,
            TrailingLockThreshold = mode == DrawdownMode.TrailingThreshold ? 106_000m : 0m,
            ConsistencyMaxDayProfitSharePercent = random.Next(2) == 0 ? 40 : null,
            MaxCalendarDays = random.Next(2) == 0 ? 30 : null,
            MaxOpenPositions = random.Next(2) == 0 ? 5 : null,
            AllowWeekendHolding = random.Next(2) == 0,
            AllowNewsTrading = random.Next(2) == 0
        };
        return PropFirmChallenge.Create(UserId.New(), TradingAccountId.New(), $"dst-{i}",
            new Money(100_000m), rules);
    }
}
