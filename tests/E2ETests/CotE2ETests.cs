using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the Commitment of Traders page through the real UI (mobile + desktop). COT data comes from the
// keyless public CFTC source, so the feature is enabled by default: the page renders its market selector,
// report-kind chips and futures/options switch, resolves to a known state (charts or the no-data notice),
// and a non-default report-kind selection takes effect — all without crashing the circuit (mandate 11).
[Collection(AppCollection.Name)]
public sealed class CotE2ETests(AppFixture app)
{
    [Fact]
    public async Task Cot_page_renders_with_controls_on_mobile()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/cot", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();

        // The report-kind chips render whether or not any data has been ingested yet.
        await page.Locator("[data-testid=cot-kind-legacy]")
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        (await page.Locator("[data-testid=cot-market]").CountAsync()).Should().BeGreaterThan(0);
        (await page.Locator("[data-testid=cot-combined]").CountAsync()).Should().BeGreaterThan(0);

        // The compare-markets selector renders whenever the feature is enabled.
        (await page.Locator("[data-testid=cot-compare]").CountAsync()).Should().BeGreaterThan(0);

        // The page carries a HelpTip explaining what the COT report shows.
        (await page.Locator("[data-testid=help-tip]").First.IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Cot_page_resolves_to_a_known_state_and_switches_report_kind()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/cot", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();

        // Never a permanent blank body — it resolves to a visible state: the latest-snapshot panel (with
        // charts) when data is present, or the no-data notice. (The market MudSelect's data-testid lands on a
        // hidden input, so it is deliberately excluded here.)
        var resolved = page.Locator("[data-testid=cot-latest], [data-testid=cot-nodata]");
        await resolved.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        (await resolved.CountAsync()).Should().BeGreaterThan(0);

        // A non-default report-kind selection must take effect without crashing the circuit.
        await page.Locator("[data-testid=cot-kind-legacy]").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await page.GetByText("Disaggregated", new PageGetByTextOptions { Exact = true }).First.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Cot_page_exports_csv_and_shows_zoom_controls_when_data_present()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/cot", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var latest = page.Locator("[data-testid=cot-latest]");
        var nodata = page.Locator("[data-testid=cot-nodata]");
        await page.Locator("[data-testid=cot-latest], [data-testid=cot-nodata]").First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

        // The working path (data present) exposes the CSV export button and the chart zoom-in/out toolbar.
        if (await latest.IsVisibleAsync())
        {
            var export = page.Locator("[data-testid=cot-export]");
            await export.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
            (await export.GetAttributeAsync("href")).Should().Contain("/api/cot/export.csv");
            (await page.Locator(".apexcharts-zoomin-icon").CountAsync())
                .Should().BeGreaterThan(0, "the charts carry zoom-in/out toolbar buttons");

            // Choosing a market to compare adds the comparison charts (selection control takes effect).
            await page.Locator(".mud-select:has([data-testid=cot-compare]) .mud-input-control").First.ClickAsync();
            var option = page.Locator(".mud-popover .mud-list-item").First;
            await option.WaitForAsync(new LocatorWaitForOptions { Timeout = 8000 });
            await option.ClickAsync();
            await page.Keyboard.PressAsync("Escape");
            await page.Locator("[data-testid=cot-net-comparison]")
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        }
        else
        {
            (await nodata.IsVisibleAsync()).Should().BeTrue();
        }
    }
}
