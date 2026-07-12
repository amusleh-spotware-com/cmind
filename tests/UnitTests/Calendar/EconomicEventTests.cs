using Core;
using Core.Calendar;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

public sealed class EconomicEventTests
{
    private static readonly DateTimeOffset Effective = new(2024, 2, 13, 13, 30, 0, TimeSpan.Zero);
    private static readonly ImpactAssessment Impact = ImpactModel.Score(new ImpactInputs(0.9, 0.004, 0.7));

    private static EconomicEvent Scheduled(DateTimeOffset now) =>
        EconomicEvent.Schedule(
            EconomicSeriesId.New(), new SeriesCode("US.CPI.MoM"), new CountryCode("US"),
            ReleaseWindow.Exact(Effective), "America/New_York", Impact, now);

    [Fact]
    public void Schedule_records_a_scheduled_revision_and_is_not_released()
    {
        var e = Scheduled(Effective.AddDays(-7));

        e.Released.Should().BeFalse();
        e.Revisions.Should().ContainSingle().Which.Kind.Should().Be(RevisionKind.Scheduled);
        e.EffectiveAt.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Release_sets_actual_and_raises_event()
    {
        var e = Scheduled(Effective.AddDays(-7));
        e.ClearDomainEvents();

        e.Release(3.1m, 3.0m, 2.9m, Impact, "%", "fred:CPIAUCSL", Effective.AddMinutes(1));

        e.Released.Should().BeTrue();
        e.LatestRevision!.Actual.Should().Be(3.1m);
        e.DomainEvents.OfType<EconomicEventReleased>().Should().ContainSingle();
    }

    [Fact]
    public void Release_before_effective_instant_is_rejected_unless_early()
    {
        var e = Scheduled(Effective.AddDays(-7));

        var act = () => e.Release(3.1m, null, null, Impact, "%", "src", Effective.AddMinutes(-1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarActualBeforeRelease);

        var early = Scheduled(Effective.AddDays(-7));
        early.Release(3.1m, null, null, Impact, "%", "src", Effective.AddMinutes(-1), earlyRelease: true);
        early.Released.Should().BeTrue();
    }

    [Fact]
    public void Cannot_release_twice()
    {
        var e = Scheduled(Effective.AddDays(-7));
        e.Release(3.1m, null, null, Impact, "%", "src", Effective);

        var act = () => e.Release(3.2m, null, null, Impact, "%", "src", Effective.AddMinutes(1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarEventTransitionInvalid);
    }

    [Fact]
    public void Revisions_are_append_only_and_monotonic_in_knownat()
    {
        var e = Scheduled(Effective.AddDays(-7));
        e.Release(3.1m, null, null, Impact, "%", "src", Effective);

        var act = () => e.Revise(3.0m, null, null, Impact, "%", "src", Effective.AddMinutes(-10));
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarRevisionOutOfOrder);

        e.Revise(3.0m, null, null, Impact, "%", "src", Effective.AddDays(30));
        e.Revisions.Should().HaveCount(3);
        e.LatestRevision!.Kind.Should().Be(RevisionKind.Revised);
    }

    [Fact]
    public void RevisionAsOf_excludes_later_known_revisions_no_look_ahead()
    {
        var e = Scheduled(Effective.AddDays(-7));
        e.Release(3.1m, null, null, Impact, "%", "src", Effective);
        e.Revise(3.4m, null, null, Impact, "%", "src", Effective.AddDays(30));

        // As the calendar looked the day after release: the first print, not the later revision.
        e.RevisionAsOf(Effective.AddDays(1))!.Actual.Should().Be(3.1m);
        e.RevisionAsOf(Effective.AddDays(60))!.Actual.Should().Be(3.4m);
        // Before we knew anything: only the scheduled prior.
        e.RevisionAsOf(Effective.AddDays(-8)).Should().BeNull();
    }

    [Fact]
    public void AdjustSchedule_moves_instant_and_records_a_reschedule_revision()
    {
        var e = Scheduled(Effective.AddDays(-7));
        var moved = Effective.AddHours(1);

        e.AdjustSchedule(moved, Impact, "src", Effective.AddDays(-1));

        e.EffectiveAt.Should().Be(moved);
        e.LatestRevision!.Kind.Should().Be(RevisionKind.Rescheduled);
        e.LatestRevision!.RescheduledInstant.Should().Be(moved);
        e.DomainEvents.OfType<EconomicEventRescheduled>().Should().ContainSingle();
    }

    [Fact]
    public void SnapshotAsOf_carries_point_in_time_impact_level()
    {
        var e = Scheduled(Effective.AddDays(-7));
        var snapshot = e.SnapshotAsOf(Effective);

        snapshot.Country.Value.Should().Be("US");
        snapshot.Series.Value.Should().Be("US.CPI.MOM");
        snapshot.Impact.Should().Be(Impact.Level);
    }
}
