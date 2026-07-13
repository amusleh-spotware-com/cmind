using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the real localized UI: the language switcher + settings language section are present, switching
// language re-renders the shell in that language, Arabic flips the document to RTL, and the choice sticks
// across a reload (cookie). Each test resets the culture to English so it never leaks into other specs.
[Collection(AppCollection.Name)]
public sealed class LocalizationTests(AppFixture app)
{
    private static async Task SwitchCultureAsync(IPage page, string culture) =>
        await page.GotoAsync($"/set-culture?culture={culture}&redirectUri=/",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

    [Fact]
    public async Task Language_switcher_and_settings_language_section_are_present()
    {
        var page = await app.NewAuthedPageAsync();
        try
        {
            await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            (await page.Locator("[data-testid=language-switcher]").CountAsync())
                .Should().BeGreaterThan(0, "the app bar must expose the language switcher");

            await page.Locator("[data-testid=nav-settings]").ClickAsync();
            await page.Locator("[data-testid=settings-section-language]")
                .WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await page.Locator("[data-testid=settings-section-language]").ClickAsync();
            await page.Locator("[data-testid=settings-language-panel]")
                .WaitForAsync(new() { State = WaitForSelectorState.Visible });
            (await page.Locator("[data-testid=settings-language-panel]").IsVisibleAsync())
                .Should().BeTrue("the settings dialog must expose a Language section");
        }
        finally { await SwitchCultureAsync(page, "en"); }
    }

    [Fact]
    public async Task Switching_to_german_renders_the_shell_in_german()
    {
        var page = await app.NewAuthedPageAsync();
        try
        {
            await SwitchCultureAsync(page, "de");

            (await page.GetAttributeAsync("html", "lang")).Should().Be("de");
            (await page.Locator("[data-testid=nav-settings]").InnerTextAsync())
                .Should().Contain("Einstellungen");

            // The choice persists across a plain reload via the culture cookie.
            await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            (await page.GetAttributeAsync("html", "lang")).Should().Be("de");
        }
        finally { await SwitchCultureAsync(page, "en"); }
    }

    [Fact]
    public async Task Switching_to_arabic_flips_the_document_to_rtl()
    {
        var page = await app.NewAuthedPageAsync();
        try
        {
            await SwitchCultureAsync(page, "ar");

            (await page.GetAttributeAsync("html", "lang")).Should().Be("ar");
            (await page.GetAttributeAsync("html", "dir")).Should().Be("rtl");
            (await page.Locator("[data-testid=nav-settings]").InnerTextAsync())
                .Should().Contain("الإعدادات");
        }
        finally { await SwitchCultureAsync(page, "en"); }
    }

    // I-05: it is not enough that <html dir=rtl> is set — assert the computed flow direction actually
    // cascades to a shell element (the nav), so a future MudBlazor RTL regression is caught programmatically
    // rather than only by eye.
    [Fact]
    public async Task Arabic_locale_lays_out_shell_right_to_left()
    {
        var page = await app.NewAuthedPageAsync();
        try
        {
            await SwitchCultureAsync(page, "ar");

            (await page.GetAttributeAsync("html", "dir")).Should().Be("rtl");

            var htmlDirection = await page.Locator("html")
                .EvaluateAsync<string>("el => getComputedStyle(el).direction");
            htmlDirection.Should().Be("rtl", "the document root must compute RTL in Arabic");

            var nav = page.Locator("[data-testid=nav-settings]");
            await nav.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            var navDirection = await nav.EvaluateAsync<string>("el => getComputedStyle(el).direction");
            navDirection.Should().Be("rtl", "a shell nav element must inherit RTL flow direction in Arabic");
        }
        finally { await SwitchCultureAsync(page, "en"); }
    }
}
