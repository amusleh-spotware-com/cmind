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

        // Creating a rule needs no AI key — it is a plain DB write, so it must appear in the Rules table.
        // Under CI load the Blazor circuit can be briefly non-interactive, so a click is silently lost.
        // Re-fill + re-click each attempt (a reload clears the form), and reload-verify in case the write
        // succeeded but the live table update was missed — both failure modes converge on the DB truth.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await page.GetByLabel("Rule name").FillAsync(name);
            await page.GetByLabel("Symbol").FillAsync("GBPUSD");
            await page.Locator("button:has-text('Create rule')").ClickAsync();
            try
            {
                await page.GetByText(name).First.WaitForAsync(new() { Timeout = 3000, State = WaitForSelectorState.Visible });
                break;
            }
            catch (Exception) when (attempt < 7)
            {
                await page.ReloadAsync();
                await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
                if (await page.GetByText(name).First.IsVisibleAsync()) break; // created, live update just missed it
            }
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
