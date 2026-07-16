using Microsoft.Playwright;

namespace E2ETests;

internal static class PageInteractionExtensions
{
    // Blazor Server interactive-circuit race: a click issued before the circuit is connected is silently
    // dropped, so the handler never runs and the awaited result never appears — the test then burns its
    // full visibility timeout and fails flakily under parallel-boot CI load. Retry the trigger click until
    // the expected element becomes visible (the same tactic DialogTests uses), then let the caller assert.
    public static async Task ClickUntilVisibleAsync(this IPage page, string clickSelector, ILocator expected,
        int attempts = 15, bool force = false)
    {
        // force bypasses Playwright's actionability wait (visible/enabled/STABLE): a MudBlazor button can
        // stay "unstable" indefinitely while the component keeps re-rendering under CI load, so a normal
        // click never fires. A forced click dispatches straight to the resolved element.
        var options = new PageClickOptions { Force = force };
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await page.ClickAsync(clickSelector, options);
                await expected.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Visible });
                return;
            }
            catch (TimeoutException) { /* circuit not interactive / result not up yet — retry */ }
            catch (PlaywrightException) { /* stale locator or click intercepted — retry */ }
        }
        // Retries exhausted: one final click so the caller's assertion surfaces the real diagnostic.
        await page.ClickAsync(clickSelector, options);
    }

    // Same interactive-circuit race, but the observable is a control becoming ENABLED (a disabled action
    // toggling on once valid input lands). A field fill issued before the circuit is connected is dropped,
    // so the disabled→enabled transition never happens; re-apply the fill action until the control enables.
    public static async Task RunUntilEnabledAsync(this IPage page, Func<Task> action, ILocator expected,
        int attempts = 15)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            await action();
            try
            {
                await Assertions.Expect(expected).ToBeEnabledAsync(new() { Timeout = 2000 });
                return;
            }
            catch (PlaywrightException) { /* circuit not interactive / fill not yet applied — retry */ }
        }
        await action();
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
