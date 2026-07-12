using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the AI features end-to-end through the real UI against a configured provider — the in-process
// fake local LLM by default (deterministic), or a real provider when AI_E2E_BASEURL is set. Because the
// fake speaks the OpenAI wire, these same tests validate every OpenAI-compatible target with no diff.
[Collection(AiLocalCollection.Name)]
public sealed class AiFeatureLocalTests(AiLocalFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 20000 };

    private async Task<IPage> OpenAsync(string route, bool mobile = false)
    {
        var page = mobile ? await app.NewAuthedMobilePageAsync() : await app.NewAuthedPageAsync();
        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        return page;
    }

    private async Task AssertOutputAsync(IPage page)
    {
        var output = page.Locator("[data-testid=ai-output]");
        await Assertions.Expect(output).ToBeVisibleAsync(Slow);
        var text = await output.InnerTextAsync();
        text.Should().NotBeNullOrWhiteSpace();
        if (app.UsingFakeLlm) text.Should().Contain(AiLocalFixture.CannedReply);
    }

    [Fact]
    public async Task Ai_is_enabled_no_not_configured_notice()
    {
        var page = await OpenAsync("/ai/review");
        await Assertions.Expect(page.Locator("button:has-text('Review cBot')")).ToBeEnabledAsync(new() { Timeout = 20000 });
        (await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Review_returns_ai_output()
    {
        var page = await OpenAsync("/ai/review");
        await page.GetByLabel("cBot source").FillAsync("public class Bot { }");
        var button = page.Locator("button:has-text('Review cBot')");
        await Assertions.Expect(button).ToBeEnabledAsync(new() { Timeout = 20000 });
        await button.ClickAsync();
        await AssertOutputAsync(page);
    }

    [Fact]
    public async Task Sentiment_returns_ai_output()
    {
        var page = await OpenAsync("/ai/sentiment");
        var button = page.Locator("button:has-text('Get sentiment')");
        await Assertions.Expect(button).ToBeEnabledAsync(new() { Timeout = 20000 });
        await button.ClickAsync();
        await AssertOutputAsync(page);
    }

    [Fact]
    public async Task Debate_returns_ai_output()
    {
        var page = await OpenAsync("/ai/debate");
        await page.GetByLabel("cBot source").FillAsync("public class Bot { }");
        var button = page.Locator("button:has-text('Run the debate')");
        await Assertions.Expect(button).ToBeEnabledAsync(new() { Timeout = 20000 });
        await button.ClickAsync();
        await AssertOutputAsync(page);
    }

    [Fact]
    public async Task Sentiment_returns_ai_output_on_mobile()
    {
        var page = await OpenAsync("/ai/sentiment", mobile: true);
        var button = page.Locator("button:has-text('Get sentiment')");
        await Assertions.Expect(button).ToBeEnabledAsync(new() { Timeout = 20000 });
        await button.ClickAsync();
        await AssertOutputAsync(page);
    }

    [Fact]
    public async Task Currency_strength_page_is_ai_enabled_and_refresh_runs()
    {
        var page = await OpenAsync("/ai/currency-strength");

        // AI is configured (fake local LLM) ⇒ no keyless gate, refresh is enabled.
        (await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync()).Should().BeFalse();
        var refresh = page.Locator("[data-testid=cs-refresh]");
        await Assertions.Expect(refresh).ToBeEnabledAsync(new() { Timeout = 20000 });

        // Trigger a refresh (exercises the AI gather + explain through the fake LLM); the page must not crash.
        await refresh.ClickAsync();
        await page.WaitForTimeoutAsync(1500);
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Settings_lists_the_active_provider()
    {
        var page = await OpenAsync("/settings/ai");
        await Assertions.Expect(page.Locator("[data-testid=ai-provider-card]").First).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=ai-active-chip]").First).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Add_provider_dialog_creates_a_second_provider()
    {
        var page = await OpenAsync("/settings/ai");
        await page.Locator("[data-testid=ai-add-provider]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid=ai-dlg-baseurl]")).ToBeVisibleAsync(Slow);

        // A keyless local OpenAI-compatible provider (kind 1 is preselected default flow).
        await page.GetByTestId("ai-dlg-baseurl").FillAsync("http://localhost:11434/v1/");
        await page.GetByTestId("ai-dlg-model").FillAsync("llama3.1:8b");
        // Do not steal "active" from the working fake provider — keep the other tests' provider intact.
        await page.Locator("[data-testid=ai-dlg-activate]").ClickAsync();
        await page.Locator("[data-testid=ai-dlg-save]").ClickAsync();

        // At least two provider cards now render.
        await Assertions.Expect(page.Locator("[data-testid=ai-provider-card]"))
            .ToHaveCountAsync(2, new() { Timeout = 20000 });
    }
}
