using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Coverage for the smaller UI additions: the cBots "Optimize" coming-soon page and the themed Blazor
// reconnect modal.
[Collection(AppCollection.Name)]
public sealed class MiscUiTests(AppFixture app)
{
    [Fact]
    public async Task Optimize_shows_themed_coming_soon()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/optimize", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var placeholder = page.Locator("[data-testid=coming-soon]");
        (await placeholder.IsVisibleAsync()).Should().BeTrue("/optimize should show the coming-soon placeholder");
        (await placeholder.InnerTextAsync()).Should().Contain("Coming soon");
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Reconnect_modal_present_and_hidden_and_themed()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var modal = page.Locator("#components-reconnect-modal");
        (await modal.CountAsync()).Should().Be(1, "the custom themed reconnect modal must be in the DOM");
        (await modal.IsVisibleAsync()).Should().BeFalse("it is hidden until the circuit drops");

        // It reads the app's surface token, proving it's the themed markup, not the default white box.
        var bg = await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.querySelector('.app-reconnect-card')).backgroundColor");
        bg.Should().NotBeNullOrEmpty();
        bg.Should().NotBe("rgb(255, 255, 255)", "the reconnect card must not be the default white");
    }
}
