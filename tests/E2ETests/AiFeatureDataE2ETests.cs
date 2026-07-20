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
        await page.WaitForAppReadyAsync();

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
        await page.WaitForAppReadyAsync();

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

    // F8 UI — Tune Advisor as a background run: seed a cBot with a completed backtest, start a tune from the
    // list dialog, and the detail page renders the AI output once the background run completes.
    [Fact]
    public async Task Tune_run_completes_in_background_for_seeded_cbot()
    {
        var page = await app.NewAuthedPageAsync();
        var seeded = await SeedAsync(page);
        var name = await CBotNameAsync(page, seeded.CBotId);

        await page.GotoAsync("/ai/tune", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        await page.Locator("[data-testid=ai-run-new]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid=ai-tune-create]")).ToBeVisibleAsync(Slow);
        await page.Locator(".mud-select:has([data-testid=ai-tune-cbot-select]) .mud-input-control").ClickAsync();
        await page.Locator($".mud-popover .mud-list-item:has-text('{name}')").First.ClickAsync();
        var create = page.Locator("[data-testid=ai-tune-create]");
        await Assertions.Expect(create).ToBeEnabledAsync(new() { Timeout = 20000 });
        await create.ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid=ai-run-title]")).ToBeVisibleAsync(Slow);
        var output = page.Locator("[data-testid=ai-run-output]");
        await Assertions.Expect(output).ToBeVisibleAsync(new() { Timeout = 30000 });
        if (app.UsingFakeLlm) (await output.InnerTextAsync()).Should().Contain(AiLocalFixture.CannedReply);
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
    }

    // F9 UI — Optimize as a background run: seed a cBot + trading account, start an optimization from the list
    // dialog, and the detail page reaches a terminal state (the canned reply yields no parseable parameter
    // sets, so the run ends Failed with an actionable message — the whole list→dialog→background→detail flow).
    [Fact]
    public async Task Optimize_run_reaches_terminal_state_in_background()
    {
        var page = await app.NewAuthedPageAsync();
        var seeded = await SeedAsync(page);
        var name = await CBotNameAsync(page, seeded.CBotId);
        var (accountNumber, _) = await AgentTestHelpers.SeedTradingAccountAsync(page, app.BaseUrl);

        await page.GotoAsync("/ai/optimize", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        await page.Locator("[data-testid=ai-run-new]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid=ai-opt-create]")).ToBeVisibleAsync(Slow);
        await page.Locator(".mud-select:has([data-testid=ai-opt-cbot-select]) .mud-input-control").ClickAsync();
        await page.Locator($".mud-popover .mud-list-item:has-text('{name}')").First.ClickAsync();
        await page.Locator(".mud-select:has([data-testid=ai-opt-account-select]) .mud-input-control").ClickAsync();
        await page.Locator($".mud-popover .mud-list-item:has-text('{accountNumber}')").First.ClickAsync();
        // Timeframe is a selector (not a free text box) — pick a non-default value from its list.
        await page.Locator(".mud-select:has([data-testid=ai-opt-timeframe]) .mud-input-control").ClickAsync();
        await page.Locator(".mud-popover .mud-list-item:has-text('m1')").First.ClickAsync();
        var create = page.Locator("[data-testid=ai-opt-create]");
        await Assertions.Expect(create).ToBeEnabledAsync(new() { Timeout = 20000 });
        await create.ClickAsync();

        // Detail page opens and the background run leaves the "working" state (Completed or Failed).
        await Assertions.Expect(page.Locator("[data-testid=ai-run-title]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=ai-run-detail-progress]"))
            .ToBeHiddenAsync(new() { Timeout = 30000 });
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
    }

    private static async Task<string> CBotNameAsync(IPage page, Guid cbotId)
    {
        var response = await page.APIRequest.GetAsync("/api/cbots/");
        using var doc = JsonDocument.Parse(await response.TextAsync());
        foreach (var el in doc.RootElement.EnumerateArray())
            if (el.GetProperty("id").GetGuid() == cbotId)
                return el.GetProperty("name").GetString()!;
        throw new InvalidOperationException("seeded cBot not found in /api/cbots/");
    }
}
