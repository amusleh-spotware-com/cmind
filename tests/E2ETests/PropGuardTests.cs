using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class PropGuardTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Prop_guard_page_renders_and_lists_load()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/prop-guard");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await Assertions.Expect(page.GetByText("New rule")).ToBeVisibleAsync(Slow);
        // /api/prop/rules returned 200 and bound an empty result.
        await Assertions.Expect(page.GetByText("No rules yet.")).ToBeVisibleAsync(Slow);
    }
}
