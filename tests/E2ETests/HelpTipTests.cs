using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Verifies the reusable inline help affordance works on mobile: tapping the help icon opens its
// tooltip (hover is unavailable on touch, so ShowOnClick must surface the guidance).
[Collection(AppCollection.Name)]
public sealed class HelpTipTests(AppFixture app)
{
    [Fact]
    public async Task Help_tip_opens_on_tap_on_mobile()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/nodes", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var tip = page.Locator("[data-testid=help-tip]").First;
        await tip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await tip.TapAsync();

        var tooltip = page.Locator(".mud-tooltip");
        await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        (await tooltip.InnerTextAsync()).Should().Contain("Nodes are the machines");
    }
}
