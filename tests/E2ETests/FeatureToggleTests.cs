using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class FeatureToggleTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Disabling_a_feature_hides_its_nav_and_404s_its_api_then_re_enabling_restores_it()
    {
        var page = await app.NewAuthedPageAsync();
        try
        {
            await page.GotoAsync("/");
            await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
            await Assertions.Expect(page.Locator("a[href='/copy-trading']")).ToBeVisibleAsync(Slow);

            var disable = await page.APIRequest.PutAsync($"{app.BaseUrl}/api/features/CopyTrading",
                new APIRequestContextOptions { DataObject = new { Enabled = false } });
            disable.Status.Should().Be(200);

            var copyWhileOff = await page.APIRequest.GetAsync($"{app.BaseUrl}/api/copy/profiles");
            copyWhileOff.Status.Should().Be(404);

            await page.GotoAsync("/");
            await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
            (await page.Locator("a[href='/copy-trading']").CountAsync()).Should().Be(0);
        }
        finally
        {
            var restore = await page.APIRequest.PutAsync($"{app.BaseUrl}/api/features/CopyTrading",
                new APIRequestContextOptions { DataObject = new { Enabled = (bool?)null } });
            restore.Status.Should().Be(200);
        }

        var copyWhenOn = await page.APIRequest.GetAsync($"{app.BaseUrl}/api/copy/profiles");
        copyWhenOn.Status.Should().Be(200);
    }

    // H-04: the feature list must show a human-readable label ("Portfolio Agent"), not the raw
    // adjacent-caps enum name ("PortfolioAgent"), and never expose a raw PascalCase identifier.
    [Fact]
    public async Task Feature_settings_shows_human_labels_not_raw_enum_names()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/settings/features", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await Assertions.Expect(page.Locator("[data-testid=feature-label]").Filter(new() { HasTextString = "Portfolio Agent" }))
            .ToBeVisibleAsync(Slow);

        var labels = await page.Locator("[data-testid=feature-label]").AllInnerTextsAsync();
        labels.Should().NotBeEmpty();
        labels.Should().NotContain("PortfolioAgent", "raw adjacent-caps enum names must not reach the UI");
        labels.Should().NotContain("EconomicCalendar");
    }
}
