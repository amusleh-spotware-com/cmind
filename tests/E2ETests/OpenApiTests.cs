using System.Net;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class OpenApiTests(AppFixture app)
{
    private static readonly string Suffix = Guid.NewGuid().ToString("N")[..6];
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Callback_with_code_but_no_state_shows_state_error_not_generic_error()
    {
        var page = await app.NewAuthedPageAsync();

        await page.GotoAsync("/openapi/callback?code=e2e_dummy_code");

        // Regression: cTrader returns only `code` (no `state`); the state now travels via cookie.
        // A cold callback (no cookie) must report the missing state specifically, not the old
        // "Missing authorization code or state." shown even when a code was present.
        await Assertions.Expect(page.GetByText("Missing authorization state")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Add_via_open_api_configures_single_app_and_redirects_to_ctrader()
    {
        var page = await app.NewAuthedPageAsync();

        // 1. No app yet: the authorize entrypoint redirects to the setup page, not to cTrader.
        await page.GotoAsync("/api/openapi/authorize");
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/settings/openapi$"));
        await Assertions.Expect(page.GetByText("No Open API application configured")).ToBeVisibleAsync(Slow);

        // 2. Configure the single application through the dialog (not an inline page section).
        var dialog = await OpenDialogAsync(page, "Add Application");
        var inputs = dialog.Locator("input");
        await inputs.Nth(0).FillAsync($"E2E-OpenApi-{Suffix}");
        await inputs.Nth(1).FillAsync($"client-{Suffix}");
        await inputs.Nth(2).FillAsync($"secret-{Suffix}");
        await SubmitAsync(dialog, "Save");

        await Assertions.Expect(page.GetByText("Configured")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText($"client-{Suffix}")).ToBeVisibleAsync(Slow);

        // 3. Authorize now issues a 302 to cTrader with client_id + state, and sets the state cookie.
        //    Replay the authenticated request without following redirects so the assertion stays local
        //    and deterministic (cTrader itself would redirect on to id.ctrader.com over the network).
        var cookies = await page.Context.CookiesAsync();
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true, CookieContainer = new CookieContainer() };
        foreach (var c in cookies)
            handler.CookieContainer.Add(new System.Net.Cookie(c.Name, c.Value, "/", "127.0.0.1"));

        using var http = new HttpClient(handler) { BaseAddress = new Uri(app.BaseUrl) };
        var resp = await http.GetAsync("/api/openapi/authorize");

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var location = resp.Headers.Location!.ToString();
        Assert.Contains("openapi.ctrader.com/apps/auth", location);
        Assert.Contains($"client_id=client-{Suffix}", location);
        Assert.Contains("redirect_uri=", location);
        Assert.Contains("scope=trading", location);
        Assert.Contains("state=", location);

        Assert.True(resp.Headers.TryGetValues("Set-Cookie", out var setCookies));
        Assert.Contains(setCookies, v => v.Contains("oapi_oauth_state"));
    }

    private static async Task<ILocator> OpenDialogAsync(IPage page, string buttonText)
    {
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        var button = page.Locator($"button:has-text('{buttonText}')").First;
        await button.WaitForAsync(new() { Timeout = 15000 });
        var dialog = page.Locator(".mud-dialog").Last;

        for (var attempt = 0; attempt < 15; attempt++)
        {
            await button.ClickAsync();
            try
            {
                await dialog.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Visible });
                return dialog;
            }
            catch (TimeoutException) { /* circuit not interactive yet — retry */ }
            catch (PlaywrightException) { /* stale locator after circuit reconnect — retry */ }
        }
        throw new TimeoutException($"Dialog did not open after clicking '{buttonText}'.");
    }

    private static async Task SubmitAsync(ILocator dialog, string buttonText)
    {
        var button = dialog.Locator($"button:has-text('{buttonText}')");
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await button.ClickAsync();
            try
            {
                await dialog.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Hidden });
                return;
            }
            catch (TimeoutException) { /* submit click lost before circuit ready — retry */ }
            catch (PlaywrightException) { /* stale locator after circuit reconnect — retry */ }
        }
        throw new TimeoutException($"Dialog did not close after clicking '{buttonText}'.");
    }
}
