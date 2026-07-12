using Core;
using Core.Calendar;
using Core.Constants;
using Core.Domain;

namespace Nodes.Alerts;

/// <summary>
/// Evaluates a calendar-driven alert rule against the economic calendar (no AI): if a matching upcoming
/// release sits within the rule's lead-time window, it raises — de-duplicated so the same release fires at
/// most once. Pure orchestration over the aggregate + the calendar read side; the caller persists.
/// </summary>
public sealed class EconomicAlertEvaluator(IEconomicCalendar calendar, TimeProvider timeProvider)
{
    public async Task<AlertEvent?> EvaluateAsync(AlertRule rule, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        rule.MarkEvaluated(now);

        var currencies = string.IsNullOrWhiteSpace(rule.Currencies)
            ? null
            : rule.Currencies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var query = new CalendarQuery
        {
            From = now,
            To = now.AddMinutes(rule.MinutesBefore ?? AlertConstants.DefaultIntervalMinutes),
            MinImpact = rule.MinImpact,
            Currencies = currencies,
            Limit = 5
        };

        var events = await calendar.GetEventsAsync(query, ct);
        var next = events.FirstOrDefault(e => !e.Released);
        if (next is null) return null;

        var severity = next.Impact == ImpactLevel.Critical
            ? AlertConstants.SeverityCritical
            : AlertConstants.SeverityWarning;
        var message = $"{next.SeriesCode} ({next.Country}) {next.Impact} impact at {next.EffectiveAt:u}";
        return rule.RaiseForEvent(next.Id, new AlertSeverity(severity), message, now);
    }
}
