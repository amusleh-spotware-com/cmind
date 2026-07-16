using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// The instance detail page must reflect state changes live (no manual reload): a run stopped from the page,
// or a backtest that finishes, should update the status chip, swap Stop→Start and (for a backtest) reveal
// the equity curve. Uses the dev-only /api/testseed to create a running instance without Docker.
[Collection(AiLocalCollection.Name)]
public sealed class InstanceLiveUpdateTests(AiLocalFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

    private static async Task<Guid> SeedRunningInstanceAsync(IPage page) => (await SeedAsync(page)).Running;

    private static async Task<(Guid CompletedBacktest, Guid Running)> SeedAsync(IPage page)
    {
        var response = await page.APIRequest.PostAsync("/api/testseed/ai-portfolio", new() { DataObject = new { } });
        response.Ok.Should().BeTrue($"seed failed: {response.Status} {await response.TextAsync()}");
        using var doc = JsonDocument.Parse(await response.TextAsync());
        return (doc.RootElement.GetProperty("completedInstanceId").GetGuid(),
                doc.RootElement.GetProperty("runningInstanceId").GetGuid());
    }

    [Fact]
    public async Task Stopping_from_the_detail_page_updates_it_live_without_reload()
    {
        var page = await app.NewAuthedPageAsync();
        var runningId = await SeedRunningInstanceAsync(page);

        await page.GotoAsync($"/instance/{runningId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // A running instance shows Stop.
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-stop]")).ToBeVisibleAsync(Slow);

        await page.ClickAsync("[data-testid=instance-detail-stop]");

        // Without a page reload, the instance becomes terminal: the Stop icon is replaced by Start.
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-start]")).ToBeVisibleAsync(Slow);
        (await page.Locator("[data-testid=instance-detail-stop]").IsVisibleAsync())
            .Should().BeFalse("a stopped instance must not still show the Stop control");
    }

    // The real bug: an instance transitions with NO user action on this page (a backtest self-completes via
    // the poller). The detail page's background poll must follow the lineage and update the UI — reproduced
    // here by transitioning the instance from outside the page.
    [Fact]
    public async Task Detail_page_follows_a_transition_triggered_elsewhere_via_polling()
    {
        var page = await app.NewAuthedPageAsync();
        var runningId = await SeedRunningInstanceAsync(page);

        await page.GotoAsync($"/instance/{runningId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-stop]")).ToBeVisibleAsync(Slow);

        // Transition the instance from OUTSIDE this page (simulating the completion poller / another client).
        var stop = await page.APIRequest.PostAsync($"/api/instances/{runningId}/stop", new());
        stop.Ok.Should().BeTrue($"external stop failed: {stop.Status}");

        // The page's background poll must follow the lineage to the new terminal instance and show Start —
        // no manual refresh.
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-start]"))
            .ToBeVisibleAsync(new() { Timeout = 30000 });
    }

    // The Edit control is only offered for a STOPPED instance (never a running one), and opens a dialog
    // prefilled with the instance's current config.
    [Fact]
    public async Task Edit_control_is_offered_only_for_a_stopped_instance_and_prefills()
    {
        var page = await app.NewAuthedPageAsync();
        var (completedBacktestId, runningRunId) = await SeedAsync(page);

        // Terminal backtest → Edit button visible; opens a dialog prefilled with the instance's symbol.
        await page.GotoAsync($"/instance/{completedBacktestId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        var edit = page.Locator("[data-testid=instance-detail-edit]");
        await Assertions.Expect(edit).ToBeVisibleAsync(Slow);
        await edit.ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid=edit-symbol]")).ToHaveValueAsync("EURUSD", new() { Timeout = 30000 });
        await page.Locator(".mud-dialog button:has-text('Cancel')").ClickAsync();

        // Running run → no Edit button (only a stopped instance can be edited).
        await page.GotoAsync($"/instance/{runningRunId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-stop]")).ToBeVisibleAsync(Slow);
        (await page.Locator("[data-testid=instance-detail-edit]").IsVisibleAsync())
            .Should().BeFalse("a running instance must not offer Edit");
    }

    // Regression: the Edit dialog's Trading account and Parameter set selectors must render the human name
    // (account number · broker, set name) when prefilled — never the raw Guid id.
    [Fact]
    public async Task Edit_dialog_shows_account_and_paramset_names_not_guids()
    {
        var page = await app.NewAuthedPageAsync();
        var seed = await page.APIRequest.PostAsync("/api/testseed/ai-portfolio", new() { DataObject = new { } });
        seed.Ok.Should().BeTrue($"seed failed: {seed.Status}");
        using var doc = JsonDocument.Parse(await seed.TextAsync());
        var completedId = doc.RootElement.GetProperty("completedInstanceId").GetGuid();
        var accountNumber = doc.RootElement.GetProperty("accountNumber").GetInt64();

        await page.GotoAsync($"/instance/{completedId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        var edit = page.Locator("[data-testid=instance-detail-edit]");
        await Assertions.Expect(edit).ToBeVisibleAsync(Slow);
        await edit.ClickAsync();

        // MudSelect renders the selected text (via ToStringFunc) as the readonly input's value.
        var account = page.Locator("[data-testid=edit-account]");
        await Assertions.Expect(account).ToHaveValueAsync(
            new System.Text.RegularExpressions.Regex($"{accountNumber} - "), new() { Timeout = 30000 });
        await Assertions.Expect(page.Locator("[data-testid=edit-paramset]")).ToHaveValueAsync(
            new System.Text.RegularExpressions.Regex("seed-params"), new() { Timeout = 30000 });
        (await account.InputValueAsync()).Should().NotMatchRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-",
            "the account selector must show the number, not a raw Guid");
    }

    // Regression: opening a just-finished backtest from the list used a STALE id (the instance transitioned
    // to a new id in the background), showing "instance not found". The list link carries the lineage id, so
    // the detail page must recover to the current instance instead.
    [Fact]
    public async Task Detail_page_recovers_via_lineage_when_the_id_is_stale()
    {
        var page = await app.NewAuthedPageAsync();
        var (completedBacktestId, _) = await SeedAsync(page);

        var detail = await (await page.APIRequest.GetAsync($"/api/instances/{completedBacktestId}")).TextAsync();
        using var doc = JsonDocument.Parse(detail);
        var lineage = doc.RootElement.GetProperty("lineageId").GetGuid();

        // Navigate with a STALE (non-existent) id but the real lineage — the page must resolve the backtest.
        await page.GotoAsync($"/instance/{Guid.NewGuid()}?lineage={lineage}",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await Assertions.Expect(page.Locator("[data-testid=instance-detail-report-json]")).ToBeVisibleAsync(Slow);
        (await page.Locator("[data-testid=instance-not-found]").IsVisibleAsync())
            .Should().BeFalse("the lineage fallback must resolve the instance, not show 'not found'");
    }

    // A completed backtest's detail page offers JSON/HTML report downloads; a run never does.
    [Fact]
    public async Task Backtest_detail_report_buttons_download_and_are_absent_for_a_run()
    {
        var page = await app.NewAuthedPageAsync();
        var (completedBacktestId, runningRunId) = await SeedAsync(page);

        await page.GotoAsync($"/instance/{completedBacktestId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var jsonBtn = page.Locator("[data-testid=instance-detail-report-json]");
        await Assertions.Expect(jsonBtn).ToBeEnabledAsync(new() { Timeout = 30000 });
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-report-html]")).ToBeEnabledAsync(new() { Timeout = 30000 });

        var download = await page.RunAndWaitForDownloadAsync(async () => await jsonBtn.ClickAsync());
        download.SuggestedFilename.Should().EndWith(".json");

        // A running RUN instance offers no report buttons (backtest-only).
        await page.GotoAsync($"/instance/{runningRunId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-stop]")).ToBeVisibleAsync(Slow);
        (await page.Locator("[data-testid=instance-detail-report-json]").IsVisibleAsync())
            .Should().BeFalse("a run has no backtest report");
    }

    // The detail page's Copy logs button must copy the instance's full console log to the clipboard.
    [Fact]
    public async Task Copy_logs_button_copies_the_console_log_to_the_clipboard()
    {
        var page = await app.NewAuthedPageAsync();
        await page.Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        var (completedBacktestId, _) = await SeedAsync(page);

        await page.GotoAsync($"/instance/{completedBacktestId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var copy = page.Locator("[data-testid=instance-detail-logs-copy]");
        await Assertions.Expect(copy).ToBeEnabledAsync(new() { Timeout = 30000 });
        await copy.ClickAsync();

        await Assertions.Expect(page.Locator(".mud-snackbar:has-text('copied')")).ToBeVisibleAsync(Slow);
        var clip = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        clip.Should().Contain("seed backtest console line 1", "the full console log is placed on the clipboard");
    }

    // The browser tab title must identify the specific instance (cBot name + symbol), so a live-run tab and a
    // backtest tab are distinguishable — not a generic "Instance".
    [Fact]
    public async Task Instance_detail_browser_title_shows_the_cbot_name_and_symbol()
    {
        var page = await app.NewAuthedPageAsync();
        var runningId = await SeedRunningInstanceAsync(page);

        await page.GotoAsync($"/instance/{runningId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-stop]")).ToBeVisibleAsync(Slow);

        await page.WaitForFunctionAsync("() => document.title.includes('EURUSD')");
        var title = await page.TitleAsync();
        title.Should().Contain("seed-bot", "the title carries the cBot name");
        title.Should().Contain("EURUSD", "the title carries the instance symbol");
    }

    // Regression: staying on a run's page must not, after a poll cycle, switch to a DIFFERENT instance
    // (e.g. a completed backtest of the same cBot). The live poll must only ever apply data for this exact
    // instance lineage.
    [Fact]
    public async Task Staying_on_a_run_page_does_not_switch_to_another_instance_of_the_same_cbot()
    {
        var page = await app.NewAuthedPageAsync();
        var (_, runningRunId) = await SeedAsync(page);

        await page.GotoAsync($"/instance/{runningRunId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-stop]")).ToBeVisibleAsync(Slow);

        // Let several 4s poll cycles run.
        await page.WaitForTimeoutAsync(10000);

        (await page.Locator("[data-testid=instance-detail-stop]").IsVisibleAsync())
            .Should().BeTrue("the run page must keep showing the running run after polling");
        (await page.Locator("[data-testid=instance-detail-start]").IsVisibleAsync())
            .Should().BeFalse("the page must not switch to a terminal (backtest) instance of the same cBot");
    }

    // Regression: Blazor reuses the InstanceDetail component when navigating /instance/{A} -> /instance/{B}
    // (only the Id param changes). A stale lineage/poll made a live-run page suddenly show the previous
    // backtest's data + equity. Reproduced here with an in-app (SPA) navigation, which reuses the component
    // — a full page load (GotoAsync) would mount a fresh component and hide the bug.
    [Fact]
    public async Task Spa_navigation_between_instances_does_not_leak_the_previous_instance_data()
    {
        var page = await app.NewAuthedPageAsync();
        var (completedBacktestId, runningRunId) = await SeedAsync(page);

        // Open the completed backtest first (terminal → shows the Start control).
        await page.GotoAsync($"/instance/{completedBacktestId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-start]")).ToBeVisibleAsync(Slow);

        // Marker to confirm the next navigation is client-side (window survives SPA nav, is wiped by a full
        // reload) — otherwise a full reload would remount a fresh component and mask the reuse bug.
        await page.EvaluateAsync("() => { window.__spaMarker = true; }");

        // Client-side (Blazor-intercepted) navigation to the running run — reuses the component.
        await page.EvaluateAsync(
            "(href) => { const a = document.createElement('a'); a.href = href; a.setAttribute('data-testid','spa-link'); a.textContent = 'go'; a.style.cssText = 'position:fixed;top:0;left:0;z-index:99999;padding:8px;background:#fff;'; document.body.appendChild(a); }",
            $"/instance/{runningRunId}");
        // Force the click: the page keeps re-rendering from the live poll, so Playwright's stability wait
        // can never settle; Blazor still intercepts the click event and navigates client-side.
        await page.ClickAsync("[data-testid=spa-link]", new() { Force = true });
        await page.WaitForURLAsync($"**/instance/{runningRunId}");
        (await page.EvaluateAsync<bool>("() => window.__spaMarker === true"))
            .Should().BeTrue("the navigation must be client-side (component reuse) — the case that triggers the leak");

        // The page must now reflect the RUNNING run (Stop control), not the previous backtest (Start).
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-stop]")).ToBeVisibleAsync(Slow);
        (await page.Locator("[data-testid=instance-detail-start]").IsVisibleAsync())
            .Should().BeFalse("the reused page must not still show the previous (backtest) instance's controls");
    }
}
