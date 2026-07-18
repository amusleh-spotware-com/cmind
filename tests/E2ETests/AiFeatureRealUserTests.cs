using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Real-user, start-to-finish coverage for the AI features that were previously only asserted at the
// "enabled + no-crash" or "not configured" level. Runs in the shared AI-configured collection so every
// call goes through the real UI/endpoint → IAiFeatureService → provider adapter (the fake local LLM by
// default, a real provider when AI_E2E_BASEURL is set). On the fake, the deterministic canned reply must
// actually render — proving the whole path, not just that the control is reachable.
[Collection(AiLocalCollection.Name)]
public sealed class AiFeatureRealUserTests(AiLocalFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

    // A 1×1 transparent PNG — enough to exercise the vision request path end to end.
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    // F14 — copy-profile recommendation, driven exactly as a user does it: type a risk profile, click
    // "AI suggest", and read the rendered recommendation. Previously only the keyless "not configured"
    // degradation was asserted; here AI is configured, so the real recommendation must render.
    [Fact]
    public async Task Copy_recommend_renders_ai_output_through_ui()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/copy-trading", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        // "AI suggest" opens the dialog; type the risk profile in its large text box, then "Suggest".
        await page.GetByTestId("ai-suggest").ClickAsync();
        await page.GetByTestId("ai-risk-profile").FillAsync("conservative");

        var recommendation = page.Locator("[data-testid=ai-recommendation]");
        await page.ClickUntilVisibleAsync("[data-testid=ai-suggest-confirm]", recommendation);

        await Assertions.Expect(recommendation).ToBeVisibleAsync(Slow);
        var text = await recommendation.InnerTextAsync();
        text.Should().NotBeNullOrWhiteSpace();
        text.Should().NotContain("not configured");
        if (app.UsingFakeLlm) text.Should().Contain(AiLocalFixture.CannedReply);
    }

    // F12 — vision-to-strategy is endpoint-only (no dedicated page); drive it through the authenticated
    // API context, which still exercises UI-auth → endpoint → IAiFeatureService → RoutingAiClient.
    //
    // The fake local LLM is an OpenAI-compatible provider, whose default capabilities do NOT include
    // vision. That is the point of coverage: RoutingAiClient must gate the image request and return the
    // typed, user-facing "does not support image input" message — a clean degradation, never a crash or a
    // raw framework error (mandate 11). Actual vision inference is only provable against a vision-capable
    // real provider (AI_E2E_BASEURL), so there we accept either a rendered answer or the same typed gate.
    [Fact]
    public async Task Vision_endpoint_degrades_cleanly_without_vision_support()
    {
        var page = await app.NewAuthedPageAsync();
        var response = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/ai/vision",
            new() { DataObject = new { MediaType = "image/png", Base64 = OnePixelPngBase64, Note = "What setup is this?" } });
        response.Status.Should().Be(200);

        using var doc = JsonDocument.Parse(await response.TextAsync());
        var success = doc.RootElement.GetProperty("success").GetBoolean();
        if (app.UsingFakeLlm)
        {
            success.Should().BeFalse("the fake OpenAI-compatible provider is text-only");
            doc.RootElement.GetProperty("error").GetString()
                .Should().Contain("image input", "vision must degrade to the typed capability message");
        }
        else if (success)
        {
            doc.RootElement.GetProperty("text").GetString().Should().NotBeNullOrWhiteSpace();
        }
        else
        {
            doc.RootElement.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    // F13 — strategy curation is endpoint-only; drive it through the authenticated API context.
    [Fact]
    public async Task Curate_endpoint_returns_ai_output()
    {
        var page = await app.NewAuthedPageAsync();
        var response = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/ai/curate",
            new() { DataObject = new { Name = "MeanReverter", Language = "CSharp", Source = "public class Bot { }" } });
        response.Status.Should().Be(200);

        using var doc = JsonDocument.Parse(await response.TextAsync());
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        var text = doc.RootElement.GetProperty("text").GetString();
        text.Should().NotBeNullOrWhiteSpace();
        if (app.UsingFakeLlm) text.Should().Contain(AiLocalFixture.CannedReply);
    }

    // F16 — Agent Studio research-desk debate: create an agent, then run the multi-analyst debate. The
    // desk calls the AI once per analyst + a reviewer synthesis; every analyst opinion and the synthesis
    // carry the model's reply, so the AI output must surface (canned reply on the fake).
    [Fact]
    public async Task Agent_studio_debate_returns_ai_output()
    {
        var page = await app.NewAuthedPageAsync();

        var create = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/agent-studio",
            new() { DataObject = new { Name = $"desk-{Guid.NewGuid():N}", Archetype = "Scalper", AccountIds = new[] { Guid.NewGuid() } } });
        create.Status.Should().Be(200, $"create agent failed: {await create.TextAsync()}");
        using var created = JsonDocument.Parse(await create.TextAsync());
        var agentId = created.RootElement.GetProperty("id").GetGuid();

        var debate = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/agent-studio/{agentId}/debate",
            new() { DataObject = new { } });
        debate.Status.Should().Be(200, $"debate failed: {await debate.TextAsync()}");

        using var doc = JsonDocument.Parse(await debate.TextAsync());
        doc.RootElement.GetProperty("opinions").GetArrayLength().Should().BeGreaterThan(0);
        var synthesis = doc.RootElement.GetProperty("synthesis").GetString();
        synthesis.Should().NotBeNullOrWhiteSpace();
        if (app.UsingFakeLlm)
        {
            synthesis.Should().Contain(AiLocalFixture.CannedReply);
            var firstOpinion = doc.RootElement.GetProperty("opinions")[0].GetProperty("opinion").GetString();
            firstOpinion.Should().Contain(AiLocalFixture.CannedReply);
        }
    }
}
