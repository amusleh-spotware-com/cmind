using System.Diagnostics;
using Core.Constants;
using Serilog.Core;
using Serilog.Events;

namespace Infrastructure.Observability;

/// <summary>
/// Stamps every log event with the current W3C trace/span id from <see cref="Activity.Current"/>
/// so compact-JSON stdout correlates to distributed traces in CloudWatch Logs Insights and Azure
/// Log Analytics even when no OTLP collector is deployed.
/// </summary>
public sealed class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null || activity.IdFormat != ActivityIdFormat.W3C)
            return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            ObservabilityDefaults.TraceIdProperty, activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            ObservabilityDefaults.SpanIdProperty, activity.SpanId.ToString()));
    }
}
