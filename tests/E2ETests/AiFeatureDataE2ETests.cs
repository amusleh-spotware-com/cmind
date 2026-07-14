using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Real-user coverage for the DATA-DEPENDENT AI features — the ones that call AI only after the user has
// real portfolio state (running/completed instances, backtest reports). Previously these were asserted at
// the "enabled + no-crash / no-data" level only, because the fixture could not produce that state without
// Docker/nodes/broker. Here the dev-only /api/testseed endpoint (enabled by AiLocalFixture) seeds a
// completed backtest + a running instance in-process, so the feature actually produces AI output — and on
// the fake LLM the deterministic canned reply must render, proving the whole path end to end.
[Collection(AiLocalCollection.Name)]
public sealed class AiFeatureDataE2ETests(AiLocalFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

    private sealed record SeededPortfolio(Guid CBotId, Guid ParamSetId, Guid CompletedInstanceId, Guid RunningInstanceId);

    // Seeds a small portfolio (cBot + param set + completed backtest with a report + running instance) for
    // the owner and returns the created ids.
    private static async Task<SeededPortfolio> SeedAsync(IPage page)
    {
        var response = await page.APIRequest.PostAsync("/api/testseed/ai-portfolio", new() { DataObject = new { } });
        response.Ok.Should().BeTrue($"seed failed: {response.Status} {await response.TextAsync()}");
        using var doc = JsonDocument.Parse(await response.TextAsync());
        var root = doc.RootElement;
        return new SeededPortfolio(
            root.GetProperty("cbotId").GetGuid(),
            root.GetProperty("paramSetId").GetGuid(),
            root.GetProperty("completedInstanceId").GetGuid(),
            root.GetProperty("runningInstanceId").GetGuid());
    }

    private async Task AssertApiAiOutputAsync(IPage page, string url)
    {
        var response = await page.APIRequest.PostAsync(url, new() { DataObject = new { } });
        response.Status.Should().Be(200, $"{url} should return 200");
        using var doc = JsonDocument.Parse(await response.TextAsync());
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue($"{url} should produce AI output with data seeded");
        var text = doc.RootElement.GetProperty("text").GetString();
        text.Should().NotBeNullOrWhiteSpace();
        if (app.UsingFakeLlm) text.Should().Contain(AiLocalFixture.CannedReply);
    }

    // F6 — portfolio digest, driven as a user: seed instances, open the page, click, read the rendered AI.
    [Fact]
    public async Task Digest_renders_ai_output_with_seeded_portfolio()
    {
        var page = await app.NewAuthedPageAsync();
        await SeedAsync(page);

        await page.GotoAsync("/ai/digest", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var button = page.Locator("button:has-text('Generate digest')");
        await Assertions.Expect(button).ToBeEnabledAsync(new() { Timeout = 20000 });
        await button.ClickAsync();

        var output = page.Locator("[data-testid=ai-output]");
        await Assertions.Expect(output).ToBeVisibleAsync(Slow);
        var text = await output.InnerTextAsync();
        text.Should().NotBeNullOrWhiteSpace();
        if (app.UsingFakeLlm) text.Should().Contain(AiLocalFixture.CannedReply);
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
    }

    // F7 — live exposure, driven as a user: the seeded running instance gives the check a live symbol.
    [Fact]
    public async Task Exposure_renders_ai_output_with_seeded_running_instance()
    {
        var page = await app.NewAuthedPageAsync();
        await SeedAsync(page);

        await page.GotoAsync("/ai/exposure", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var button = page.Locator("button:has-text('Check live exposure')");
        await Assertions.Expect(button).ToBeEnabledAsync(new() { Timeout = 20000 });
        await button.ClickAsync();

        var output = page.Locator("[data-testid=ai-output]");
        await Assertions.Expect(output).ToBeVisibleAsync(Slow);
        var text = await output.InnerTextAsync();
        text.Should().NotBeNullOrWhiteSpace();
        if (app.UsingFakeLlm) text.Should().Contain(AiLocalFixture.CannedReply);
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
    }

    // F10 — backtest analysis of the seeded completed backtest (it carries a report).
    [Fact]
    public async Task Analyze_backtest_returns_ai_output_for_seeded_report()
    {
        var page = await app.NewAuthedPageAsync();
        var seeded = await SeedAsync(page);
        await AssertApiAiOutputAsync(page, $"/api/ai/analyze-backtest/{seeded.CompletedInstanceId}");
    }

    // F8 — strategy decay / tune advice from the seeded cBot's completed backtest.
    [Fact]
    public async Task Tune_advice_returns_ai_output_for_seeded_cbot()
    {
        var page = await app.NewAuthedPageAsync();
        var seeded = await SeedAsync(page);
        await AssertApiAiOutputAsync(page, $"/api/ai/tune-advice/{seeded.CBotId}");
    }

    // F9 — optimize: propose parameter sets for the seeded cBot.
    [Fact]
    public async Task Optimize_params_returns_ai_output_for_seeded_cbot()
    {
        var page = await app.NewAuthedPageAsync();
        var seeded = await SeedAsync(page);
        await AssertApiAiOutputAsync(page, $"/api/ai/optimize-params/{seeded.CBotId}");
    }

    // F11 — post-mortem of a seeded instance.
    [Fact]
    public async Task Post_mortem_returns_ai_output_for_seeded_instance()
    {
        var page = await app.NewAuthedPageAsync();
        var seeded = await SeedAsync(page);
        await AssertApiAiOutputAsync(page, $"/api/ai/post-mortem/{seeded.CompletedInstanceId}");
    }
}
