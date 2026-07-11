using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Real user journeys on emulated phones: a full create-round-trip through a dialog, and the shell across
// several device profiles. Behaves like an actual user tapping through the app.
[Collection(AppCollection.Name)]
public sealed class MobileJourneyTests(AppFixture app)
{
    public static IEnumerable<object[]> Devices() => new[]
    {
        "iPhone 13", "Pixel 5", "iPhone SE", "Galaxy S9+",
    }.Select(d => new object[] { d });

    [Theory]
    [MemberData(nameof(Devices))]
    public async Task Shell_renders_without_overflow_across_devices(string device)
    {
        var page = await app.NewAuthedMobilePageAsync(device);
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator("[data-testid=bottom-nav]").IsVisibleAsync())
            .Should().BeTrue($"{device}: bottom nav must show on a phone");
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse($"{device}: no error UI");

        var fits = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth <= window.innerWidth + 1");
        fits.Should().BeTrue($"{device}: dashboard must not scroll sideways");
    }

    [Fact]
    public async Task Create_mcp_key_round_trip_on_mobile()
    {
        var label = $"e2e-key-{DateTimeOffset.UtcNow.Ticks}";
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/mcp", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await page.GetByRole(AriaRole.Button, new() { Name = "New Key" }).ClickAsync();

        var dialog = page.Locator(".mud-dialog").First;
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await dialog.Locator("input").First.FillAsync(label);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        // The one-time key reveal dialog confirms creation.
        await page.GetByText("shown only once").WaitForAsync(new() { Timeout = 8000 });
        await page.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();

        // The new key shows in the list as a labelled card.
        await page.GetByText(label).First.WaitForAsync(new() { Timeout = 8000 });
        (await page.GetByText(label).First.IsVisibleAsync()).Should().BeTrue();
    }
}
