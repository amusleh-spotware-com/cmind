using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Regression for I-04: with no trading account connected, the dashboard showed KPI zeros and no guidance.
// Mandate 11 — an empty dependency state must show an actionable notice, not a silent blank/zero page.
[Collection(AppCollection.Name)]
public sealed class DashboardEmptyStateTests(AppFixture app)
{
    [Fact]
    public async Task Dashboard_with_no_accounts_shows_an_actionable_connect_notice()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await page.Locator("[data-testid=dash-no-accounts]").WaitForAsync(new() { Timeout = 10000 });
        (await page.Locator("[data-testid=dash-no-accounts]").IsVisibleAsync())
            .Should().BeTrue("the dashboard must guide the user to connect an account when none exists");
        (await page.Locator("[data-testid=dash-connect-account]").GetAttributeAsync("href"))
            .Should().Be("/accounts", "the notice must link to where accounts are connected");
    }
}
