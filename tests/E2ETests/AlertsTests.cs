using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class AlertsTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Alerts_page_renders_and_create_rule_round_trips()
    {
        var page = await OpenAsync();

        await Assertions.Expect(page.GetByText("New rule")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText("No alerts raised yet.")).ToBeVisibleAsync(Slow);

        var name = $"E2E rule {Guid.NewGuid():N}".Substring(0, 20);
        await page.GetByLabel("Rule name").FillAsync(name);
        await page.GetByLabel("Symbol").FillAsync("GBPUSD");

        // Creating a rule needs no AI key — it is a plain DB write, so it must appear in the Rules table.
        var createButton = page.Locator("button:has-text('Create rule')");
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await createButton.ClickAsync();
            try
            {
                await page.GetByText(name).First.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Visible });
                break;
            }
            catch (TimeoutException) { /* circuit not interactive yet — retry */ }
            catch (PlaywrightException) { /* stale after reconnect — retry */ }
        }
        await Assertions.Expect(page.GetByText(name).First).ToBeVisibleAsync(Slow);
    }

    private async Task<IPage> OpenAsync()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/alerts");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        return page;
    }
}
