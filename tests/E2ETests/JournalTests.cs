using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class JournalTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Journal_renders_for_a_new_account()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/journal");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // A fresh account has no runs/backtests → the coaching empty state renders (proves the wired path).
        await Assertions.Expect(page.Locator("[data-testid=journal-empty], [data-testid=journal-summary]").First)
            .ToBeVisibleAsync(Slow);
    }
}
