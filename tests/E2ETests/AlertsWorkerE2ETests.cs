using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// F18 — market-watch alerts, driven through the REAL background worker (AlertEvaluator), not a stub. The
// fixture enables the worker with a fast poll; the fake local LLM returns a valid alert-assessment JSON
// (alert raised) for the alerting prompt, embedding the canned reply in the message. Creating a rule and
// waiting for the worker to evaluate it must produce an AlertEvent whose AI-authored message renders — the
// alerts feature proven end to end, exactly as a user experiences it.
[Collection(AiWorkersCollection.Name)]
public sealed class AlertsWorkerE2ETests(AiWorkersFixture app)
{
    [Fact]
    public async Task Market_watch_rule_raises_ai_authored_alert_event()
    {
        var page = await app.NewAuthedPageAsync();

        var create = await page.APIRequest.PostAsync("/api/alerts/rules",
            new() { DataObject = new { Name = $"watch-{Guid.NewGuid():N}", Symbol = "EURUSD", IntervalMinutes = 5, Enabled = true } });
        create.Ok.Should().BeTrue($"create rule failed: {create.Status} {await create.TextAsync()}");

        // The worker polls every ~2s; wait (with margin) for it to evaluate the new rule and raise an event.
        string? message = null;
        for (var attempt = 0; attempt < 40 && message is null; attempt++)
        {
            await page.WaitForTimeoutAsync(1500);
            var events = await page.APIRequest.GetAsync("/api/alerts/events?unacknowledged=true");
            if (!events.Ok) continue;
            using var doc = JsonDocument.Parse(await events.TextAsync());
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                var text = e.GetProperty("message").GetString();
                if (!string.IsNullOrWhiteSpace(text)) { message = text; break; }
            }
        }

        message.Should().NotBeNull("the alert worker must raise an event for the market-watch rule");
        if (app.UsingFakeLlm) message.Should().Contain(AiLocalFixture.CannedReply);
    }
}
