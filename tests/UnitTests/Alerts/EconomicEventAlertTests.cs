using Core;
using Core.Calendar;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Alerts;

public sealed class EconomicEventAlertTests
{
    private static readonly DateTimeOffset Now = new(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateEconomicEvent_sets_the_trigger_and_config()
    {
        var rule = AlertRule.CreateEconomicEvent(
            UserId.New(), "CPI watch", ImpactLevel.High, 60, "USD,EUR", new EvaluationInterval(15));

        rule.Trigger.Should().Be(AlertTriggerKind.EconomicEvent);
        rule.MinImpact.Should().Be(ImpactLevel.High);
        rule.MinutesBefore.Should().Be(60);
        rule.Currencies.Should().Be("USD,EUR");
    }

    [Fact]
    public void RaiseForEvent_dedups_the_same_event_but_fires_for_a_new_one()
    {
        var rule = AlertRule.CreateEconomicEvent(
            UserId.New(), "watch", ImpactLevel.High, 60, null, new EvaluationInterval(15));
        var eventId = CalendarEventId.New();

        rule.RaiseForEvent(eventId, new AlertSeverity("warning"), "m", Now).Should().NotBeNull();
        rule.RaiseForEvent(eventId, new AlertSeverity("warning"), "m", Now.AddMinutes(1)).Should().BeNull();
        rule.Events.Should().ContainSingle();

        rule.RaiseForEvent(CalendarEventId.New(), new AlertSeverity("critical"), "m2", Now.AddMinutes(2))
            .Should().NotBeNull();
        rule.Events.Should().HaveCount(2);
    }

    [Fact]
    public void CreateEconomicEvent_rejects_out_of_range_lead_time()
    {
        var act = () => AlertRule.CreateEconomicEvent(
            UserId.New(), "watch", ImpactLevel.High, -1, null, new EvaluationInterval(15));
        act.Should().Throw<DomainException>().Which.Code.Should().Be(Core.Constants.DomainErrors.IntervalOutOfRange);
    }
}
