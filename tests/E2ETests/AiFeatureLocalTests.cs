using System.Text.RegularExpressions;
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
        await page.WaitForAppReadyAsync();
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
    public async Task Model_selector_renders_and_feature_runs_on_chosen_model()
    {
        // Every AI feature page exposes a model selector (seeded fake provider is the default). It renders,
        // and running the feature with a model selected still returns AI output through the chosen model.
        var page = await OpenAsync("/ai/review");
        // MudSelect exposes the testid on its (hidden) value input; a non-empty GUID value proves the
        // selector rendered and pre-selected a usable model (the seeded fake provider, the default).
        var selector = page.Locator("[data-testid=ai-model-select]");
        await Assertions.Expect(selector).ToHaveValueAsync(
            new Regex("[0-9a-fA-F-]{36}"), new() { Timeout = 20000 });

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

        // AI is configured (fake local LLM) ⇒ no keyless gate; the owner sees the refresh enabled.
        (await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync()).Should().BeFalse();
        var refresh = page.Locator("[data-testid=cs-refresh]");
        await Assertions.Expect(refresh).ToBeEnabledAsync(new() { Timeout = 20000 });

        // Trigger a refresh (exercises the AI gather + explain through the fake LLM); the page must not crash.
        await refresh.ClickAsync();
        await page.WaitForTimeoutAsync(1500);
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();

        // G-05: after a successful refresh the AI narrative must render and carry the fake LLM's canned reply.
        var narrative = page.Locator("[data-testid=cs-narrative]");
        await Assertions.Expect(narrative).ToBeVisibleAsync(Slow);
        if (app.UsingFakeLlm)
            (await narrative.InnerTextAsync()).Should().Contain(AiLocalFixture.CannedReply);
    }

    [Fact]
    public async Task Currency_strength_refresh_is_disabled_for_non_owner()
    {
        // G-01: the refresh POST is Owner-only; a non-owner must never see an enabled (doomed) button.
        var email = $"cs-user-{Guid.NewGuid():N}@e2e.local";
        const string password = "User_Pass_123!";
        const string newPassword = "User_Pass_456!";

        var ownerPage = await app.NewAuthedPageAsync();
        var create = await ownerPage.APIRequest.PostAsync($"{app.BaseUrl}/api/users",
            new APIRequestContextOptions { DataObject = new { Email = email, Password = password, Role = 2 } });
        create.Status.Should().Be(200);

        var context = await app.Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = app.BaseUrl });
        try
        {
            var login = await context.APIRequest.PostAsync($"{app.BaseUrl}/api/auth/login",
                new APIRequestContextOptions { DataObject = new { Email = email, Password = password } });
            login.Status.Should().Be(200);
            var change = await context.APIRequest.PostAsync($"{app.BaseUrl}/api/auth/change-password",
                new APIRequestContextOptions
                { DataObject = new { CurrentPassword = password, NewPassword = newPassword } });
            change.Status.Should().Be(200);

            var page = await context.NewPageAsync();
            await page.GotoAsync("/ai/currency-strength", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await page.WaitForAppReadyAsync();

            var refresh = page.Locator("[data-testid=cs-refresh]");
            await Assertions.Expect(refresh).ToBeVisibleAsync(Slow);
            await Assertions.Expect(refresh).ToBeDisabledAsync(new() { Timeout = 20000 });
            (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Currency_strength_has_no_horizontal_overflow_on_mobile_with_data()
    {
        // G-04: after a refresh loads the (multi-column) tables, the phone layout must not scroll sideways.
        var page = await OpenAsync("/ai/currency-strength", mobile: true);

        var refresh = page.Locator("[data-testid=cs-refresh]");
        await Assertions.Expect(refresh).ToBeEnabledAsync(new() { Timeout = 20000 });
        await refresh.ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid=cs-ranking]")).ToBeVisibleAsync(Slow);

        var noOverflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth <= window.innerWidth + 1");
        noOverflow.Should().BeTrue("the currency-strength tables must collapse to cards on a 360px phone");
    }

    // E-07: every AI feature page is driven end-to-end through the real UI against the fake LLM.
    //
    // /ai/digest, /ai/exposure, /ai/tune and /ai/optimize call AI only after the user has real portfolio
    // data (running/completed instances, a linked trading account, an available node) — none of which can
    // be seeded deterministically in-process (they need Docker/nodes/broker creds, which the fixture lacks).
    // So for those four we assert the AI-configured contract the fake LLM lets us prove deterministically:
    // the keyless notice is absent, the action is enabled, and driving it never crashes the circuit and
    // never surfaces a raw framework error — the empty-data path returns a clean, user-facing outcome.
    // (The canned-reply render for these is covered by the data-independent pages: Review/Sentiment/Debate.)

    [Fact]
    public async Task Build_page_is_ai_enabled_and_runs()
    {
        var page = await OpenAsync("/ai/build");
        (await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync()).Should().BeFalse();

        await page.GetByLabel("Describe your strategy").FillAsync("RSI mean-reversion on EURUSD h1");
        var button = page.Locator("button:has-text('Build my bot')");
        await Assertions.Expect(button).ToBeEnabledAsync(new() { Timeout = 20000 });
        await button.ClickAsync();

        // The AI generates the bot (canned reply as source) and the build pipeline runs; the result panel
        // must render (success or a build-log failure) without tripping the error UI.
        await Assertions.Expect(page.Locator("[data-testid=ai-build-result]")).ToBeVisibleAsync(new() { Timeout = 120000 });
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();

        // The build log is always shown with a Copy button; the canned reply is not valid source, so the
        // build fails — the failed project is still saved to the user's cBots with an "open in editor" link.
        await Assertions.Expect(page.Locator("[data-testid=ai-build-copy-log]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=ai-build-saved]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=ai-build-open-failed]")).ToBeVisibleAsync(Slow);
    }

    [Theory]
    [InlineData("/ai/digest", "Generate digest")]
    [InlineData("/ai/exposure", "Check live exposure")]
    public async Task Portfolio_ai_page_is_enabled_and_runs_clean(string route, string action)
    {
        var page = await OpenAsync(route);
        (await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync()).Should().BeFalse();

        var button = page.Locator($"button:has-text('{action}')");
        await Assertions.Expect(button).ToBeEnabledAsync(new() { Timeout = 20000 });
        await button.ClickAsync();
        await page.WaitForTimeoutAsync(1500);

        // No portfolio data seeded ⇒ the endpoint returns a clean "no data" result; the page must not crash.
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse($"{route} crashed the circuit");
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse($"{route} tripped the ErrorBoundary");
    }

    [Fact]
    public async Task Journal_ai_coach_is_enabled_and_runs_clean()
    {
        var page = await OpenAsync("/journal");
        (await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync()).Should().BeFalse();

        // The journal now exposes an AI Coach (reusing the digest) with a model selector; it is enabled when
        // AI is configured and must run without crashing even with no seeded portfolio data.
        var coach = page.Locator("[data-testid=journal-coach-run]");
        await Assertions.Expect(coach).ToBeVisibleAsync(Slow);
        await Assertions.Expect(coach).ToBeEnabledAsync(new() { Timeout = 20000 });
        await coach.ClickAsync();
        await page.WaitForTimeoutAsync(1500);

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Tune_page_is_ai_enabled()
    {
        var page = await OpenAsync("/ai/tune");
        (await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync()).Should().BeFalse();
        await Assertions.Expect(page.Locator("button:has-text('Check for decay')")).ToBeEnabledAsync(new() { Timeout = 20000 });
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Optimize_page_is_ai_enabled()
    {
        var page = await OpenAsync("/ai/optimize");
        (await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync()).Should().BeFalse();
        await Assertions.Expect(page.Locator("button:has-text('Propose')")).ToBeEnabledAsync(new() { Timeout = 20000 });
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
    }

    // E-04 working path: with AI configured, /agent-studio enables the per-row Start control (no gate).
    [Fact]
    public async Task Agent_studio_start_enabled_when_ai_present()
    {
        var page = await app.NewAuthedPageAsync();
        var (accountNumber, _) = await AgentTestHelpers.SeedTradingAccountAsync(page, app.BaseUrl);
        await page.GotoAsync("/agent-studio", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();
        (await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync()).Should().BeFalse();

        await page.Locator("[data-testid=agent-new]").ClickAsync();
        await page.GetByLabel("Agent name").FillAsync($"live-{Guid.NewGuid():N}");
        await AgentTestHelpers.SelectManagedAccountAsync(page, accountNumber);
        await page.Locator("[data-testid=agent-create-submit]").ClickAsync();

        var start = page.Locator("[data-testid=agent-start]").First;
        await start.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 8000 });
        await Assertions.Expect(start).ToBeEnabledAsync(new() { Timeout = 8000 });
    }

    // E-09 working path: with AI configured, /agent enables the Create mandate button.
    [Fact]
    public async Task Agent_create_mandate_enabled_when_ai_present()
    {
        var page = await OpenAsync("/agent");
        (await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync()).Should().BeFalse();
        await Assertions.Expect(page.Locator("[data-testid=agent-create-mandate]")).ToBeEnabledAsync(new() { Timeout = 20000 });
    }

    // E-08: with real rows, the /agent-studio, /agent and /alerts tables must collapse to cards on a
    // 360px phone — no horizontal overflow (MobileLayoutTests runs on an empty DB and never hits this).
    private static async Task AssertNoHorizontalOverflowAsync(IPage page)
    {
        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1");
        overflow.Should().BeFalse("the page must not scroll horizontally at 360px with data");
    }

    [Fact]
    public async Task Agent_studio_table_has_no_overflow_on_mobile_with_data()
    {
        var page = await app.NewAuthedMobilePageAsync();
        var (accountNumber, _) = await AgentTestHelpers.SeedTradingAccountAsync(page, app.BaseUrl);
        await page.GotoAsync("/agent-studio", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();
        await page.Locator("[data-testid=agent-new]").ClickAsync();
        await page.GetByLabel("Agent name").FillAsync($"m-{Guid.NewGuid():N}");
        await AgentTestHelpers.SelectManagedAccountAsync(page, accountNumber);
        await page.Locator("[data-testid=agent-create-submit]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid=agents-table]")).ToBeVisibleAsync(new() { Timeout = 8000 });
        await page.WaitForTimeoutAsync(300);
        await AssertNoHorizontalOverflowAsync(page);
    }

    [Fact]
    public async Task Alerts_table_has_no_overflow_on_mobile_with_data()
    {
        var page = await OpenAsync("/alerts", mobile: true);
        var api = page.APIRequest;
        var create = await api.PostAsync("/api/alerts/rules",
            new() { DataObject = new { Name = $"m-{Guid.NewGuid():N}", Symbol = "EURUSD", IntervalMinutes = 60, Enabled = true } });
        create.Ok.Should().BeTrue($"seed alert rule failed: {create.Status}");
        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForTimeoutAsync(300);
        await AssertNoHorizontalOverflowAsync(page);
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

    [Fact]
    public async Task Add_provider_dialog_offers_a_kimi_moonshot_preset()
    {
        var page = await OpenAsync("/settings/ai");
        await page.Locator("[data-testid=ai-add-provider]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid=ai-dlg-baseurl]")).ToBeVisibleAsync(Slow);

        // Switch to the OpenAI-compatible kind so the preset picker (which includes Kimi) appears.
        await page.Locator("div.mud-select:has([data-testid=ai-dlg-kind])").First.ClickAsync();
        await page.Locator(".mud-list-item:has-text('OpenAI-compatible')").First.ClickAsync();

        // Pick the Kimi (Moonshot) preset → the base URL auto-fills to the Moonshot endpoint.
        await page.Locator("div.mud-select:has([data-testid=ai-dlg-preset])").First.ClickAsync();
        await page.Locator(".mud-list-item:has-text('Kimi')").First.ClickAsync();

        await Assertions.Expect(page.GetByTestId("ai-dlg-baseurl"))
            .ToHaveValueAsync(new Regex("moonshot"), new() { Timeout = 10000 });
        await page.GetByTestId("ai-dlg-cancel").ClickAsync();
    }
}
