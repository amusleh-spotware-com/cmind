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
    // /instance/{id}. In Blazor Server the page's HttpClient call runs server-side (Playwright cannot mock
    // it) and a successful run needs a node + docker (the live tier, CBotRealRunBacktestTests). Here we
    // assert the deterministic reachable branch: with no node available, quick-run fails, so the user stays
    // on /cbots and sees an error — never a spurious navigation or a crash. The success navigation itself
    // is a single guarded NavigateTo added by the fix and is exercised by the live run/backtest tier.
    [Fact]
    public async Task Quick_run_without_a_node_stays_on_cbots_and_reports_the_failure()
    {
        var page = await app.NewAuthedPageAsync();
        await CreateProjectAsync(page, $"run-nav-{Suffix}", "C#");

        await page.GotoAsync("/cbots", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // The Run (Play) icon is the third action button in the row (Build, Edit, Run, Tune, Delete).
        var row = page.Locator($"tr:has-text('run-nav-{Suffix}')");
        await row.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        var play = row.Locator("button:has(.mud-icon-root)").Nth(2);

        var error = page.Locator(".mud-snackbar:has-text('Run failed')");
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try { await play.ClickAsync(new() { Timeout = 2000 }); }
            catch (PlaywrightException) { /* stale after reconnect — retry */ }
            try { await error.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 2000 }); break; }
            catch (TimeoutException) { /* circuit not interactive yet — retry */ }
        }

        (await error.First.IsVisibleAsync())
            .Should().BeTrue("a failed quick-run must surface an error, not silently no-op");
        page.Url.Should().EndWith("/cbots", "a failed quick-run must not navigate away from /cbots (B-09)");
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse("a failed quick-run must not crash the circuit");
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
