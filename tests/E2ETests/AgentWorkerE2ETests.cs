using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// F15 — the portfolio agent, driven through the REAL background worker (PortfolioAgentService). A
// Suggest-autonomy mandate makes the worker call the AI and RECORD a proposal (it never executes —
// execution is Auto-only), so there is a clean, side-effect-free assertion surface: the AgentProposal.
// The fixture runs the worker on a fast cadence; the fake local LLM returns a valid agent-action JSON with
// the canned reply in its reasoning. Creating a mandate and waiting for the worker must produce a proposal
// whose AI-authored reasoning renders — the agent feature proven end to end.
[Collection(AiWorkersCollection.Name)]
public sealed class AgentWorkerE2ETests(AiWorkersFixture app)
{
    [Fact]
    public async Task Suggest_mandate_produces_ai_authored_proposal()
    {
        var page = await app.NewAuthedPageAsync();

        // Seed a cBot to attach the mandate to (no Docker — terminal/authoring state only).
        var seed = await page.APIRequest.PostAsync("/api/testseed/ai-portfolio", new() { DataObject = new { } });
        seed.Ok.Should().BeTrue($"seed failed: {seed.Status} {await seed.TextAsync()}");
        using var seeded = JsonDocument.Parse(await seed.TextAsync());
        var cbotId = seeded.RootElement.GetProperty("cbotId").GetGuid();

        var create = await page.APIRequest.PostAsync("/api/agent/mandates",
            new()
            {
                DataObject = new
                {
                    CBotId = cbotId,
                    Name = $"mandate-{Guid.NewGuid():N}",
                    Objective = "reduce drawdown",
                    Autonomy = "Suggest",
                    Enabled = true
                }
            });
        create.Ok.Should().BeTrue($"create mandate failed: {create.Status} {await create.TextAsync()}");
        using var created = JsonDocument.Parse(await create.TextAsync());
        var mandateId = created.RootElement.GetProperty("id").GetGuid();

        // The worker runs every ~2s; wait (with margin) for it to propose against the new mandate.
        string? reasoning = null;
        for (var attempt = 0; attempt < 40 && reasoning is null; attempt++)
        {
            await page.WaitForTimeoutAsync(1500);
            var proposals = await page.APIRequest.GetAsync($"/api/agent/proposals?mandateId={mandateId}");
            if (!proposals.Ok) continue;
            using var doc = JsonDocument.Parse(await proposals.TextAsync());
            foreach (var p in doc.RootElement.EnumerateArray())
            {
                var text = p.GetProperty("reasoning").GetString();
                if (!string.IsNullOrWhiteSpace(text)) { reasoning = text; break; }
            }
        }

        reasoning.Should().NotBeNull("the agent worker must record a proposal for the enabled mandate");
        if (app.UsingFakeLlm) reasoning.Should().Contain(AiLocalFixture.CannedReply);
    }
}
