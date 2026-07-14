using FluentAssertions;
using Xunit;

namespace E2ETests;

// F17 — the AI risk-guard, driven through the REAL background worker (AiRiskGuard) in assessment-only
// mode. Seeding a running instance and waiting for the worker must produce an AI risk assessment over that
// instance — the risk-guard's AI path proven end to end, non-destructively (no instance is stopped). The
// assessment is emitted to the app log (the feature's current surface), which the fixture captures; the
// summary carries the fake LLM's canned reply.
[Collection(AiRiskGuardCollection.Name)]
public sealed class RiskGuardWorkerE2ETests(AiRiskGuardFixture app)
{
    [Fact]
    public async Task Running_instance_gets_ai_risk_assessment()
    {
        var page = await app.NewAuthedPageAsync();

        var seed = await page.APIRequest.PostAsync("/api/testseed/ai-portfolio", new() { DataObject = new { } });
        seed.Ok.Should().BeTrue($"seed failed: {seed.Status} {await seed.TextAsync()}");

        // The guard scans every ~2s; wait for it to assess the seeded running instance and log the summary.
        var assessed = false;
        for (var attempt = 0; attempt < 30 && !assessed; attempt++)
        {
            await page.WaitForTimeoutAsync(1000);
            assessed = app.AppLog.Contains("AI risk-guard assessed", StringComparison.Ordinal);
        }

        assessed.Should().BeTrue("the risk-guard worker must assess the running instance");
        if (app.UsingFakeLlm) app.AppLog.Should().Contain(AiLocalFixture.CannedReply);
    }
}
