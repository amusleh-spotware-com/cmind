using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the redesigned login screen like a real user: reveal toggle, invalid-credentials feedback, a
// successful sign-in, and the responsive split-screen (hero on desktop, card-only on mobile).
[Collection(AppCollection.Name)]
public sealed class LoginTests(AppFixture app)
{
    [Fact]
    public async Task Reveal_button_toggles_password_visibility()
    {
        var page = await app.NewAnonymousPageAsync();
        await page.GotoAsync("/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var pwd = page.Locator("#app-login-password");
        (await pwd.GetAttributeAsync("type")).Should().Be("password");

        await page.Locator("#app-login-reveal").ClickAsync();
        (await pwd.GetAttributeAsync("type")).Should().Be("text");

        await page.Locator("#app-login-reveal").ClickAsync();
        (await pwd.GetAttributeAsync("type")).Should().Be("password");
    }

    [Fact]
    public async Task Invalid_credentials_show_an_error()
    {
        var page = await app.NewAnonymousPageAsync();
        await page.GotoAsync("/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await page.FillAsync("input[name=Email]", AppFixture.OwnerEmail);
        await page.FillAsync("input[name=Password]", "definitely-the-wrong-password");
        await page.ClickAsync("button.app-login-button");

        await page.WaitForURLAsync(u => u.Contains("error"));
        (await page.Locator("[data-testid=login-error]").IsVisibleAsync())
            .Should().BeTrue("an invalid sign-in must surface a visible error");
    }

    [Fact]
    public async Task Valid_credentials_sign_in()
    {
        var page = await app.NewAnonymousPageAsync();
        await page.GotoAsync("/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await page.FillAsync("input[name=Email]", AppFixture.OwnerEmail);
        await page.FillAsync("input[name=Password]", AppFixture.OwnerPassword);
        await page.ClickAsync("button.app-login-button");

        await page.WaitForURLAsync($"{app.BaseUrl}/");
        (await page.Locator(".mud-appbar").CountAsync()).Should().BeGreaterThan(0, "sign-in should land on the app shell");
    }

    [Fact]
    public async Task Hero_shows_on_desktop()
    {
        var page = await app.NewAnonymousPageAsync();
        await page.GotoAsync("/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator(".app-auth-hero").IsVisibleAsync())
            .Should().BeTrue("the branded hero panel shows on wide screens");
    }

    [Fact]
    public async Task Hero_hidden_on_mobile_no_overflow()
    {
        var page = await app.NewAnonymousMobilePageAsync();
        await page.GotoAsync("/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator(".app-auth-hero").IsVisibleAsync())
            .Should().BeFalse("the hero is desktop-only; mobile shows the card alone");
        (await page.Locator(".app-login-card").IsVisibleAsync())
            .Should().BeTrue("the login card must render on mobile");

        var fits = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth <= window.innerWidth + 1");
        fits.Should().BeTrue("login must not scroll sideways on a phone");
    }
}
