using Microsoft.Playwright;

namespace E2ETests;

internal static class PageInteractionExtensions
{
    // Blazor Server interactive-circuit race: a click issued before the circuit is connected is silently
    // dropped, so the handler never runs and the awaited result never appears — the test then burns its
    // full visibility timeout and fails flakily under parallel-boot CI load. Retry the trigger click until
    // the expected element becomes visible (the same tactic DialogTests uses), then let the caller assert.
    public static async Task ClickUntilVisibleAsync(this IPage page, string clickSelector, ILocator expected,
        int attempts = 15)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            await page.ClickAsync(clickSelector);
            try
            {
                await expected.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Visible });
                return;
            }
            catch (TimeoutException) { /* circuit not interactive yet — retry */ }
            catch (PlaywrightException) { /* stale locator after circuit reconnect — retry */ }
        }
        // Retries exhausted: one final click so the caller's assertion surfaces the real diagnostic.
        await page.ClickAsync(clickSelector);
    }
}
