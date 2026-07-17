using Microsoft.Playwright;

namespace E2ETests;

public static class PageExtensions
{
    // Wait until the Blazor Server circuit is INTERACTIVE (event handlers wired), not merely until the
    // framework script loaded. Clicking a visible-but-not-yet-interactive control silently drops the event,
    // so the old `window.Blazor !== undefined` probe let a freshly-loaded page's first click be lost — the
    // single biggest source of random "dialog/locator never appeared" E2E flakes (a different page hit the
    // window each CI run). The app sets `window.__appInteractive` on its first interactive render
    // (Routes.OnAfterRenderAsync). If that flag never arrives (unexpected), fall back to the old
    // script-present guarantee so this can never hang a test it replaced — strictly safer, never worse.
    public static async Task WaitForAppReadyAsync(this IPage page)
    {
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        try
        {
            await page.WaitForFunctionAsync("() => window.__appInteractive === true", null,
                new PageWaitForFunctionOptions { Timeout = 20_000 });
        }
        catch (TimeoutException)
        {
            // Interactivity flag never set — proceed on the framework-script guarantee, as before.
        }
    }
}
