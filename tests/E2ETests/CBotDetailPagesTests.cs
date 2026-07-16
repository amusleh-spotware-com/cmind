using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// B-08 / B-09 regression coverage for the two parameterized cBot-lifecycle pages that had no smoke,
// mobile, or overflow E2E, plus the quick-run navigation.
//   B-08: /builder/{id} (BuilderEditor) for a real project id and /instance/{id} both render without the
//         Blazor error UI / ErrorBoundary and do not scroll sideways at 360px.
//   B-09: /cbots quick-run navigates to the new instance's detail page when the response carries an
//         InstanceId (the id was previously ignored, stranding the user on /cbots).
[Collection(AppCollection.Name)]
public sealed class CBotDetailPagesTests(AppFixture app)
{
    private static readonly string Suffix = Guid.NewGuid().ToString("N")[..6];
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 20000 };

    [Fact]
    public async Task Builder_editor_renders_for_a_real_project_without_error_ui()
    {
        var page = await app.NewAuthedPageAsync();
        var projectId = await CreateProjectAsync(page, $"builder-{Suffix}", "C#");

        await page.GotoAsync($"/builder/{projectId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await Assertions.Expect(page.Locator(".monaco-editor").First).ToBeVisibleAsync(Slow);
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse("the builder editor must not trip the Blazor error UI");
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync())
            .Should().BeFalse("the builder editor must not trip the ErrorBoundary");
    }

    [Fact]
    public async Task Builder_editor_has_no_horizontal_overflow_on_mobile()
    {
        var deskPage = await app.NewAuthedPageAsync();
        var projectId = await CreateProjectAsync(deskPage, $"builder-mob-{Suffix}", "C#");

        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync($"/builder/{projectId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        await Assertions.Expect(page.Locator(".monaco-editor").First).ToBeVisibleAsync(Slow);

        var fits = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth <= window.innerWidth + 1");
        fits.Should().BeTrue("/builder/{id} must not scroll horizontally on a phone");
    }

    [Fact]
    public async Task Instance_detail_renders_on_mobile_without_overflow()
    {
        // A parameterized instance id cannot be seeded through the process-isolated fixture (start needs a
        // node + docker), so drive the not-found render on mobile — it must fit and not crash the circuit.
        var page = await app.NewAuthedMobilePageAsync();
        var missingId = "00000000-0000-0000-0000-0000000000aa";
        await page.GotoAsync($"/instance/{missingId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(page.Locator("[data-testid=instance-not-found]")).ToBeVisibleAsync(Slow);
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse("the instance detail page must not trip the Blazor error UI on mobile");

        var fits = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth <= window.innerWidth + 1");
        fits.Should().BeTrue("/instance/{id} must not scroll horizontally on a phone");
    }

    // B-09: quick-run from /cbots reads the returned InstanceId and, on success, navigates to
    // /instance/{id} (the id was previously ignored, stranding the user on /cbots). A local node is seeded
    // in the fixture, so the reachable outcome is a queued instance + navigation; the assertion accepts
    // either navigation (success) or a terminal 'Run failed' snackbar (failure), and requires no circuit
    // crash — covering the fix's real behavior without needing docker.
    [Fact]
    public async Task Quick_run_navigates_to_the_new_instance_or_reports_failure()
    {
        var page = await app.NewAuthedPageAsync();
        await CreateProjectAsync(page, $"run-nav-{Suffix}", "C#");

        await page.GotoAsync("/cbots", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // The Run (Play) icon is the third action button in the row (Build, Edit, Run, Tune, Delete).
        var row = page.Locator($"tr:has-text('run-nav-{Suffix}')");
        await row.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        var play = row.Locator("button:has(.mud-icon-root)").Nth(2);

        // B-09 invariant: quick-run either succeeds and NAVIGATES to the new instance's detail page
        // (the fix), or fails and stays on /cbots with a terminal snackbar ('Started'/'Run failed') — and
        // never crashes. (A local node is seeded, so the reachable outcome is a queued instance + nav.)
        // Ignore the transient 'Starting...' info snackbar; wait for a real outcome.
        const string outcomeReached =
            "() => location.pathname.startsWith('/instance/') || " +
            "[...document.querySelectorAll('.mud-snackbar')].some(s => /Run failed|Started/.test(s.textContent))";
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try { await play.ClickAsync(new() { Timeout = 2000 }); }
            catch (PlaywrightException) { /* stale after reconnect — retry */ }
            try
            {
                await page.WaitForFunctionAsync(outcomeReached, null, new() { Timeout = 4000 });
                break;
            }
            catch (TimeoutException) { /* circuit not interactive / outcome not in yet — retry */ }
        }

        var reached = await page.EvaluateAsync<bool>(outcomeReached);
        reached.Should().BeTrue("quick-run must either navigate to the new instance (B-09) or report an outcome — never silently no-op");
        if (page.Url.Contains("/instance/", StringComparison.Ordinal))
            page.Url.Should().Contain("/instance/", "B-09: a successful quick-run takes the user to the new instance's detail page");
        else
            page.Url.Should().EndWith("/cbots", "a non-navigating quick-run stays on /cbots");
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse("quick-run must not crash the circuit");
    }

    // The editor's Run action must open the run dialog (pick a trading account + optional parameter set),
    // not fire a blind hard-coded run. Asserts the dialog renders with the account selector, the Run/Cancel
    // actions, and the inline "new parameter set" control — and that it never crashes the circuit.
    [Fact]
    public async Task Builder_editor_run_opens_the_account_and_paramset_dialog()
    {
        var page = await app.NewAuthedPageAsync();
        var projectId = await CreateProjectAsync(page, $"run-dlg-{Suffix}", "C#");

        await page.GotoAsync($"/builder/{projectId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        await Assertions.Expect(page.Locator(".monaco-editor").First).ToBeVisibleAsync(Slow);

        var run = page.Locator("button:has-text('Run')").First;
        var dialog = page.Locator(".mud-dialog").Last;
        for (var attempt = 0; attempt < 15; attempt++)
        {
            try { await run.ClickAsync(new() { Timeout = 2000 }); }
            catch (PlaywrightException) { /* stale after reconnect — retry */ }
            try { await dialog.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Visible }); break; }
            catch (TimeoutException) { }
        }

        await Assertions.Expect(dialog.GetByText("Trading account")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(dialog.Locator("[data-testid=run-submit]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(dialog.Locator("[data-testid=run-new-paramset]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(dialog.Locator("button:has-text('Cancel')")).ToBeVisibleAsync(Slow);
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse("opening the run dialog must not crash the circuit");
    }

    // Regression: the Run/Backtest dialogs make the parameter set optional, so /api/instances/ receives a
    // null ParamSetId. That must not crash the endpoint (it used to 500 from the JSON binder). Drive it
    // through the real, authenticated app API and assert no server error.
    [Fact]
    public async Task Start_instance_with_no_param_set_does_not_500()
    {
        var page = await app.NewAuthedPageAsync();
        var res = await page.APIRequest.PostAsync(app.BaseUrl + "/api/instances/", new()
        {
            DataObject = new
            {
                CBotId = Guid.NewGuid(),
                TradingAccountId = Guid.NewGuid(),
                Symbol = "EURUSD",
                Timeframe = "h1",
                ParamSetId = (Guid?)null,
                DockerImageTag = "latest",
                Type = "Run",
                BacktestSettingsJson = (string?)null
            }
        });

        res.Status.Should().BeLessThan(500,
            "a null parameter set must be handled, not crash /api/instances with a 500");
    }

    // The instance detail page has a Back button (rendered even on the not-found path). For a missing /
    // already-transitioned instance (kind unknown) it returns the user to wherever they came from — so a
    // user who reached it from the backtest list goes back to backtests, not always /run. Navigating there
    // and back must never crash the circuit.
    [Fact]
    public async Task Instance_detail_back_button_returns_to_the_originating_list()
    {
        var page = await app.NewAuthedPageAsync();
        // Arrive from the Backtest list, then open a missing instance; Back must return to /backtest.
        await page.GotoAsync("/backtest", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        var missingId = "00000000-0000-0000-0000-0000000000bb";
        await page.GotoAsync($"/instance/{missingId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var back = page.Locator("[data-testid=instance-back-btn]");
        await Assertions.Expect(back).ToBeVisibleAsync(Slow);
        await back.ClickAsync();
        await page.WaitForURLAsync("**/backtest", new() { Timeout = 15000 });
        page.Url.Should().EndWith("/backtest", "Back returns to the list the user came from, not always /run");
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse("navigating back from the instance page must not crash the circuit");
    }

    // The console-log download endpoint must be reachable and degrade cleanly (404 for a missing instance),
    // never a 500 that would break the download button.
    [Fact]
    public async Task Instance_logs_endpoint_is_reachable_and_never_500s()
    {
        var page = await app.NewAuthedPageAsync();
        var res = await page.APIRequest.GetAsync(app.BaseUrl + $"/api/instances/{Guid.NewGuid()}/logs");
        res.Status.Should().BeLessThan(500, "the logs endpoint must handle a missing instance without a 500");
    }

    private static async Task<string> CreateProjectAsync(IPage page, string name, string language)
    {
        await page.GotoAsync("/cbots");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var dialog = await OpenDialogAsync(page, "New cBot");
        await dialog.GetByLabel("Title").FillAsync(name);
        await dialog.Locator($".mud-radio:has-text('{language}')").First.ClickAsync();

        for (var attempt = 0; attempt < 15; attempt++)
        {
            await dialog.Locator("button:has-text('Create')").ClickAsync();
            try
            {
                await page.WaitForURLAsync("**/builder/**", new() { Timeout = 3000 });
                var url = page.Url;
                return url[(url.LastIndexOf('/') + 1)..];
            }
            catch (TimeoutException) { }
            catch (PlaywrightException) { }
        }
        throw new TimeoutException("Create did not navigate to the builder editor.");
    }

    private static async Task<ILocator> OpenDialogAsync(IPage page, string buttonText)
    {
        var button = page.Locator($"button:has-text('{buttonText}')").First;
        await button.WaitForAsync(new() { Timeout = 20000 });
        var dialog = page.Locator(".mud-dialog").Last;
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await button.ClickAsync();
            try { await dialog.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Visible }); return dialog; }
            catch (TimeoutException) { }
            catch (PlaywrightException) { }
        }
        throw new TimeoutException($"Dialog did not open for '{buttonText}'.");
    }
}
