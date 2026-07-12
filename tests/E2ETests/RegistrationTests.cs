using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// The stock deployment ships with self-registration OFF, so the login page must not advertise sign-up and
// the /register page must render its branded "registration closed" state rather than a form.
[Collection(AppCollection.Name)]
public sealed class RegistrationTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Login_has_no_create_account_link_by_default()
    {
        var page = await app.NewAnonymousPageAsync();
        await page.GotoAsync("/login");
        (await page.Locator("[data-testid=login-register-link]").CountAsync())
            .Should().Be(0, "registration is disabled by default");
    }

    [Fact]
    public async Task Register_page_shows_closed_state_when_disabled()
    {
        var page = await app.NewAnonymousPageAsync();
        await page.GotoAsync("/register", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // The page renders (branded auth shell) and JS reveals the closed notice because the config
        // endpoint 404s while the feature is off.
        await Assertions.Expect(page.Locator("[data-testid=register-page]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=register-closed]")).ToBeVisibleAsync(Slow);
        (await page.Locator("[data-testid=register-form]").IsVisibleAsync()).Should().BeFalse();
    }
}
