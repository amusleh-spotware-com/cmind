using Core;
using Core.Calendar;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace StressTests.Calendar;

// Deterministic-simulation stress for the EconomicEvent aggregate: one seed drives a randomized, hostile
// stream of release/revise/reschedule/rescore operations with mostly-increasing but occasionally
// out-of-order KnownAt. The aggregate owns every guard; the harness only asserts the append-only + PIT
// invariants hold under any interleaving: revisions stay monotonic in KnownAt with dense sequence numbers,
// a rejected op never mutates state, release-once is sticky, and asOf reads are reproducible and never leak
// a later-known revision (no look-ahead). A failure prints its seed to reproduce the run.
public sealed class CalendarEventDstTests
{
    private const int Steps = 400;
    private static readonly DateTimeOffset Start = new(2024, 1, 10, 13, 30, 0, TimeSpan.Zero);
    private static readonly ImpactAssessment Impact = ImpactModel.Score(new ImpactInputs(0.9, 0.004, 0.7));

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(99)]
    [InlineData(1234)]
    [InlineData(31337)]
    [InlineData(2026)]
    public void Randomized_operation_streams_keep_the_revision_chain_consistent(int seed)
    {
        var random = new Random(seed);
        var economicEvent = EconomicEvent.Schedule(
            EconomicSeriesId.New(), new SeriesCode("US.CPI"), new CountryCode("US"),
            ReleaseWindow.Exact(Start), "UTC", Impact, Start);

        var lastKnownAt = Start;
        decimal? lastActual = null;

        for (var step = 0; step < Steps; step++)
        {
            // Mostly advance KnownAt; occasionally emit an out-of-order timestamp the aggregate must reject.
            var knownAt = random.Next(100) < 15
                ? lastKnownAt - TimeSpan.FromMinutes(random.Next(1, 5000))
                : lastKnownAt + TimeSpan.FromMinutes(random.Next(1, 5000));

            var revisionsBefore = economicEvent.Revisions.Count;
            var releasedBefore = economicEvent.Released;
            var latestBefore = economicEvent.LatestRevision?.Actual;
            var actual = Math.Round((decimal)(random.NextDouble() * 10), 2);
            var acceptedActual = lastActual; // only release/revise change the effective actual

            var accepted = true;
            try
            {
                if (!economicEvent.Released)
                {
                    economicEvent.Release(actual, null, null, Impact, "%", "src", knownAt,
                        earlyRelease: knownAt < economicEvent.EffectiveAt);
                    acceptedActual = actual;
                }
                else
                {
                    switch (random.Next(3))
                    {
                        case 0:
                            economicEvent.Revise(actual, null, null, Impact, "%", "src", knownAt);
                            acceptedActual = actual;
                            break;
                        case 1:
                            economicEvent.AdjustSchedule(
                                economicEvent.EffectiveAt + TimeSpan.FromMinutes(random.Next(1, 60)),
                                Impact, "src", knownAt);
                            break;
                        default:
                            economicEvent.RescoreImpact(Impact, knownAt);
                            break;
                    }
                }
            }
            catch (DomainException)
            {
                accepted = false;
            }

            if (accepted)
            {
                economicEvent.Revisions.Count.Should().Be(revisionsBefore + 1, $"seed {seed} step {step}");
                lastKnownAt = knownAt > lastKnownAt ? knownAt : lastKnownAt;
                lastActual = acceptedActual;
            }
            else
            {
                // A rejected operation must leave the aggregate exactly as it was.
                economicEvent.Revisions.Count.Should().Be(revisionsBefore, $"seed {seed} step {step} rejected");
                economicEvent.Released.Should().Be(releasedBefore);
                economicEvent.LatestRevision?.Actual.Should().Be(latestBefore);
            }

            AssertInvariants(economicEvent, seed, step);
        }

        // Append-only + terminal-consistent: the last revision that carries an actual is the last accepted
        // one (reschedule/rescore revisions legitimately carry no new actual).
        economicEvent.Revisions.LastOrDefault(r => r.Actual is not null)?.Actual.Should().Be(lastActual);
    }

    private static void AssertInvariants(EconomicEvent economicEvent, int seed, int step)
    {
        var revisions = economicEvent.Revisions;

        for (var i = 0; i < revisions.Count; i++)
        {
            revisions[i].Sequence.Should().Be(i, $"seed {seed} step {step}: sequence must be dense");
            if (i > 0)
                revisions[i].KnownAt.Should().BeOnOrAfter(revisions[i - 1].KnownAt,
                    $"seed {seed} step {step}: KnownAt must be monotonic");
        }

        // Point-in-time: as-of any instant, the resolved revision was known by then and is reproducible.
        var asOf = revisions[^1].KnownAt - TimeSpan.FromMinutes(1);
        var first = economicEvent.RevisionAsOf(asOf);
        var second = economicEvent.RevisionAsOf(asOf);
        first.Should().BeSameAs(second);
        if (first is not null)
            first.KnownAt.Should().BeOnOrBefore(asOf, $"seed {seed} step {step}: no look-ahead");
    }
}
