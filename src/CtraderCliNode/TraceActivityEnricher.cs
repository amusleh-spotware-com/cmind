using System.Diagnostics;
using Core.Constants;
using Serilog.Core;
using Serilog.Events;

namespace ExternalNode;

/// <summary>
/// Stamps every log event with the current W3C trace/span id from <see cref="Activity.Current"/>.
/// Duplicated from Infrastructure so the node agent stays free of the EF/Docker dependency graph.
/// </summary>
public sealed class TraceActivityEnricher : ILogEventEnricher
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
