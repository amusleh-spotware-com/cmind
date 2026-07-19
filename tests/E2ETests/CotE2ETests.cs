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
}
