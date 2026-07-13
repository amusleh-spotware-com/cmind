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

    // Same race, but for a multi-step trigger (e.g. flip a mode toggle THEN click analyze): re-run the whole
    // action each attempt so a first step dropped before the circuit was interactive is re-applied, not just
    // the final click. Without this, a lost toggle click leaves the page computing in the wrong mode and the
    // expected result never renders.
    public static async Task RunUntilVisibleAsync(this IPage page, Func<Task> action, ILocator expected,
        int attempts = 15)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            await action();
            try
            {
                await expected.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Visible });
                return;
            }
            catch (TimeoutException) { /* circuit not interactive yet — retry */ }
            catch (PlaywrightException) { /* stale locator after circuit reconnect — retry */ }
        }
        await action();
    }
}
