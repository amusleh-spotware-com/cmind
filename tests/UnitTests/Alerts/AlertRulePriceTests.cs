using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Alerts;

// Invariants + transitions for the market-watch AlertRule path and event raising/dedup, complementing
// EconomicEventAlertTests. (WS-1 Core backfill.)
public class AlertRulePriceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 11, 0, 0, TimeSpan.Zero);

    private static AlertRule NewRule() =>
        AlertRule.Create(UserId.New(), "eurusd-watch", new Symbol("EURUSD"), new EvaluationInterval(15));

    [Fact]
    public void Create_is_a_market_watch_rule()
    {
        var rule = NewRule();
        rule.Symbol.Should().Be("EURUSD");
        rule.Enabled.Should().BeTrue();
        rule.Trigger.Should().Be(AlertTriggerKind.MarketWatch);
        rule.Events.Should().BeEmpty();
    }

    [Fact]
    public void Create_rejects_a_blank_name()
    {
        var act = () => AlertRule.Create(UserId.New(), " ", new Symbol("EURUSD"), new EvaluationInterval(15));
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Config_setters_and_enable_disable_mutate_state()
    {
        var rule = NewRule();

        rule.Rename("renamed");
        rule.SetSymbol(new Symbol("GBPUSD"));
        rule.SetInterval(new EvaluationInterval(30));
        rule.MarkEvaluated(Now);
        rule.Disable();

        rule.Name.Should().Be("renamed");
        rule.Symbol.Should().Be("GBPUSD");
        rule.IntervalMinutes.Should().Be(30);
        rule.LastEvaluatedAt.Should().Be(Now);
        rule.Enabled.Should().BeFalse();

        rule.Enable();
        rule.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Rename_rejects_a_blank_name()
    {
        var rule = NewRule();
        var act = () => rule.Rename("");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Raise_adds_an_acknowledgeable_event_and_stamps_time()
    {
        var rule = NewRule();

        var evt = rule.Raise(AlertSeverity.Info, "crossed 1.10", Now);

        rule.Events.Should().ContainSingle().Which.Should().BeSameAs(evt);
        evt.Message.Should().Be("crossed 1.10");
        rule.LastEvaluatedAt.Should().Be(Now);

        evt.Acknowledged.Should().BeFalse();
        evt.Acknowledge();
        evt.Acknowledged.Should().BeTrue();
    }

    [Fact]
    public void Raise_on_a_disabled_rule_throws()
    {
        var rule = NewRule();
        rule.Disable();

        var act = () => rule.Raise(AlertSeverity.Info, "x", Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AlertRuleDisabled);
    }

    [Fact]
    public void Raise_for_event_dedupes_the_same_calendar_release()
    {
        var rule = NewRule();
        var eventId = new CalendarEventId(Guid.NewGuid());

        var first = rule.RaiseForEvent(eventId, AlertSeverity.Info, "NFP soon", Now);
        first.Should().NotBeNull();

        var second = rule.RaiseForEvent(eventId, AlertSeverity.Info, "NFP soon", Now.AddMinutes(1));
        second.Should().BeNull("the same release must alert at most once");
        rule.Events.Should().ContainSingle();
    }
}
