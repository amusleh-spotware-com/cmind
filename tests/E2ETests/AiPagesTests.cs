using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Covers the split-out AI feature pages (one per former Assistant tab) and the "AI not configured" notice
// that now appears on every AI page including Portfolio Agent, Alerts, and MCP Keys. The test app has no
// AI key, so the notice banner must show and no page may trip the ErrorBoundary.
[Collection(AppCollection.Name)]
public sealed class AiPagesTests(AppFixture app)
{
    public static IEnumerable<object[]> AiFeatureRoutes() => new[]
    {
        "/ai/build", "/ai/review", "/ai/debate", "/ai/sentiment",
        "/ai/exposure", "/ai/digest", "/ai/tune", "/ai/optimize",
    }.Select(r => new object[] { r });

    public static IEnumerable<object[]> NoticeRoutes() => new[]
    {
        // /mcp is intentionally absent: MCP key creation is AI-independent, so the page must NOT
        // carry the AI-not-configured notice (asserted by Mcp_page_has_no_ai_notice).
        "/ai/build", "/agent", "/alerts", "/agent-studio",
    }.Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(AiFeatureRoutes))]
    public async Task Ai_feature_page_renders_without_error(string route)
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator("[data-testid=page-error]").IsVisibleAsync())
            .Should().BeFalse($"{route} tripped the ErrorBoundary");
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse($"{route} tripped the Blazor error UI");
        (await page.Locator("h5, .mud-typography-h5").First.IsVisibleAsync())
            .Should().BeTrue($"{route} did not render its heading");
    }

    [Theory]
    [MemberData(nameof(NoticeRoutes))]
    public async Task Ai_page_shows_not_configured_notice(string route)
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var banner = page.Locator("[data-testid=ai-not-configured]").First;
        await banner.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 8000 });

        // The notice is provider-agnostic now (built-in model / local LLM / cloud) — it must not name a
        // single vendor, and it must point users at Settings → AI to set up a provider.
        var text = await banner.InnerTextAsync();
        text.Should().Contain("AI provider").And.Contain("Settings → AI");
        text.Should().NotContainEquivalentOf("Anthropic");
    }

    [Fact]
    public async Task Ai_not_configured_dialog_is_provider_agnostic()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/ai/build", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // The one-time "AI not configured" message box must not name a single vendor now that the app
        // supports the built-in model, local LLMs, and multiple cloud providers.
        var dialog = page.Locator(".mud-dialog:has-text('AI not configured')").First;
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        var text = await dialog.InnerTextAsync();
        text.Should().Contain("AI provider").And.Contain("built-in model");
        text.Should().NotContainEquivalentOf("Anthropic");

        await page.Locator("button:has-text('Later')").First.ClickAsync(new() { Timeout = 8000 });
    }

    // E-03: MCP keys are AI-independent — the page must not show the AI-not-configured notice, and a key
    // can be created with no AI provider present.
    [Fact]
    public async Task Mcp_page_has_no_ai_notice()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/mcp", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(page.Locator("h5, .mud-typography-h5").First).ToBeVisibleAsync();
        (await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync())
            .Should().BeFalse("MCP keys page must not show the AI-not-configured notice");
        (await page.Locator(".mud-dialog").IsVisibleAsync())
            .Should().BeFalse("no blocking AI dialog on /mcp when AI is unconfigured");
    }

    // E-04: /agent-studio must gate the Start control when no AI provider is configured — the notice shows
    // and every per-row Start button is disabled so an agent can never be "started" into a silent no-op.
    [Fact]
    public async Task Agent_studio_gates_start_when_ai_absent()
    {
        var page = await app.NewAuthedPageAsync();
        // An agent needs a managed account at creation — seed one before loading the page.
        var (accountNumber, _) = await AgentTestHelpers.SeedTradingAccountAsync(page, app.BaseUrl);
        await page.GotoAsync("/agent-studio", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var notice = page.Locator("[data-testid=ai-not-configured]").First;
        await notice.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 8000 });

        // Create an agent so a Start control renders, then assert it is disabled with AI absent.
        await page.Locator("[data-testid=agent-new]").ClickAsync();
        await page.GetByLabel("Agent name").FillAsync($"gate-{Guid.NewGuid():N}");
        await AgentTestHelpers.SelectManagedAccountAsync(page, accountNumber);
        await page.Locator("[data-testid=agent-create-submit]").ClickAsync();

        var start = page.Locator("[data-testid=agent-start]").First;
        await start.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 8000 });
        await Assertions.Expect(start).ToBeDisabledAsync(new() { Timeout = 8000 });
    }

    // E-09: /agent must not offer a create action that silently produces a mandate that never runs — the
    // Create mandate button is disabled while no AI provider is configured.
    [Fact]
    public async Task Agent_create_mandate_disabled_when_ai_absent()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/agent", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var notice = page.Locator("[data-testid=ai-not-configured]").First;
        await notice.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 8000 });

        await Assertions.Expect(page.Locator("[data-testid=agent-create-mandate]"))
            .ToBeDisabledAsync(new() { Timeout = 8000 });
    }

    [Fact]
    public async Task Old_assistant_route_is_gone()
    {
        var page = await app.NewAuthedPageAsync();
        var response = await page.GotoAsync("/assistant");
        response!.Status.Should().Be(404, "the nested AI/Assistant page was replaced by per-feature pages");
    }
}
