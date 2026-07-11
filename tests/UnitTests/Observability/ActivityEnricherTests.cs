using System.Diagnostics;
using Core.Constants;
using ExternalNode;
using FluentAssertions;
using Infrastructure.Observability;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace UnitTests.Observability;

public sealed class ActivityEnricherTests
{
    private static LogEvent NewEvent() => new(
        DateTimeOffset.UnixEpoch, LogEventLevel.Information, exception: null,
        new MessageTemplate("t", []), []);

    private sealed class TestPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
            => new(name, new ScalarValue(value));
    }

    private static string ScalarString(LogEvent evt, string name)
        => ((ScalarValue)evt.Properties[name]).Value!.ToString()!;

    [Fact]
    public void Enrich_NoActivity_AddsNothing()
    {
        Activity.Current = null;
        var evt = NewEvent();

        new ActivityEnricher().Enrich(evt, new TestPropertyFactory());

        evt.Properties.Should().BeEmpty();
    }

    [Fact]
    public void Enrich_W3CActivity_StampsTraceAndSpanIds()
    {
        using var activity = new Activity("op");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        var evt = NewEvent();

        new ActivityEnricher().Enrich(evt, new TestPropertyFactory());

        ScalarString(evt, ObservabilityDefaults.TraceIdProperty).Should().Be(activity.TraceId.ToString());
        ScalarString(evt, ObservabilityDefaults.SpanIdProperty).Should().Be(activity.SpanId.ToString());
    }

    [Fact]
    public void NodeAgentEnricher_W3CActivity_StampsTraceAndSpanIds()
    {
        using var activity = new Activity("op");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        var evt = NewEvent();

        new TraceActivityEnricher().Enrich(evt, new TestPropertyFactory());

        ScalarString(evt, ObservabilityDefaults.TraceIdProperty).Should().Be(activity.TraceId.ToString());
        ScalarString(evt, ObservabilityDefaults.SpanIdProperty).Should().Be(activity.SpanId.ToString());
    }
}
