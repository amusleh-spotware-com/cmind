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
        (await placeholder.InnerTextAsync()).Should().ContainEquivalentOf("coming soon");
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

    // Every .NET 10 reconnect state that should be visible must overlay full-screen (position:fixed),
    // never render inline in the page flow — including the terminal "session expired" states.
    [Theory]
    [InlineData("components-reconnect-show")]
    [InlineData("components-reconnect-retrying")]
    [InlineData("components-reconnect-failed")]
    [InlineData("components-reconnect-paused")]
    [InlineData("components-reconnect-rejected")]
    [InlineData("components-reconnect-resume-failed")]
    public async Task Reconnect_modal_overlays_full_screen_for_every_visible_state(string stateClass)
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Blazor toggles reconnect state by setting the element's class list; style keys off the #id.
        await page.EvaluateAsync(
            $"() => {{ document.getElementById('components-reconnect-modal').className = '{stateClass}'; }}");

        var modal = page.Locator("#components-reconnect-modal");
        (await modal.IsVisibleAsync()).Should().BeTrue($"{stateClass} must show the modal");

        var position = await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.getElementById('components-reconnect-modal')).position");
        position.Should().Be("fixed", $"{stateClass} must overlay the app, not render inline in the page flow");

        var zIndex = await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.getElementById('components-reconnect-modal')).zIndex");
        int.Parse(zIndex).Should().BeGreaterThan(1000, "the overlay must sit above the app shell/drawer");
    }

    [Fact]
    public async Task Reconnect_modal_session_expired_state_shows_reload_and_hides_spinner()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // "Session could not be resumed" is the terminal resume-failed state in .NET 10.
        await page.EvaluateAsync(
            "() => { document.getElementById('components-reconnect-modal').className = 'components-reconnect-resume-failed'; }");

        (await page.Locator("#components-reconnect-modal").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator(".app-reconnect-when-rejected").First.IsVisibleAsync())
            .Should().BeTrue("the session-expired copy must show");
        (await page.Locator(".app-reconnect-when-show").First.IsVisibleAsync()).Should().BeFalse();
        (await page.Locator(".app-reconnect-when-failed").First.IsVisibleAsync()).Should().BeFalse();
        (await page.GetByRole(AriaRole.Button, new() { Name = "Reload" }).IsVisibleAsync()).Should().BeTrue();
        (await page.Locator(".app-reconnect-spinner").IsVisibleAsync())
            .Should().BeFalse("a terminal state has no spinner");
    }

    [Fact]
    public async Task Reconnect_modal_failed_state_shows_only_failed_copy_and_retry()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await page.EvaluateAsync(
            "() => { document.getElementById('components-reconnect-modal').className = 'components-reconnect-failed'; }");

        (await page.Locator(".app-reconnect-when-failed").First.IsVisibleAsync()).Should().BeTrue();
        (await page.Locator(".app-reconnect-when-show").First.IsVisibleAsync()).Should().BeFalse();
        (await page.Locator(".app-reconnect-when-rejected").First.IsVisibleAsync()).Should().BeFalse();
        (await page.GetByRole(AriaRole.Button, new() { Name = "Retry now" }).IsVisibleAsync()).Should().BeTrue();
    }
}
