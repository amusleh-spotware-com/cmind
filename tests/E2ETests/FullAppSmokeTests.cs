using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace E2ETests;

// One long "real user" smoke walk: signed in as the owner, visit EVERY page, and on each page open
// every dialog-launching control, poke every non-destructive clickable, then Cancel/close out — creating,
// editing, deleting NOTHING. After each interaction it asserts the circuit is still alive (no Blazor error
// UI, no tripped ErrorBoundary) and that no stray dialog is left open. This is the manual-clicking pass a
// human would do to confirm "the app doesn't break during normal navigation", captured as a single run.
[Collection(AppCollection.Name)]
public sealed class FullAppSmokeTests(AppFixture app, ITestOutputHelper output)
{
    // Every page in the app — reuse the single source of truth (PageSmokeTests.Routes(), itself guarded by
    // RouteCoverageTests to cover every @page). One list to keep in sync; this walk follows it automatically.
    private static readonly string[] Routes =
        PageSmokeTests.Routes().Select(r => (string)r[0]).ToArray();

    // Buttons whose whole point is to open a dialog we can safely Cancel out of. Matched case-insensitively
    // against the button's text; anything not on this list is left untouched so we never fire an action that
    // mutates state (Delete/Stop/Start/Run/Save/Create/Submit/Reset/Rotate/Revoke/Logout live off-list).
    private static readonly Regex DialogOpeners = new(
        @"\b(new|add|create|edit|customize|configure|manage|filter|invite|details?|view|open|change|set up|setup|generate|connect|link|backtest new|run new)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Text that means "this button acts / mutates" — never click these, even if they slip past the opener list.
    private static readonly Regex Destructive = new(
        @"\b(delete|remove|stop|kill|start|run\b|save|submit|confirm|create|add\b|reset|rotate|revoke|logout|sign out|sign-out|apply|update|enable|disable|pause|resume|approve|reject|send|upload|download|import|export|restore|purge|clear|deactivate|activate)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Ways a dialog offers to back out, in preference order. Escape is the universal fallback.
    private static readonly string[] CloseLabels =
        ["Cancel", "Close", "Back", "Dismiss", "Not now", "Later", "Maybe later", "No"];

    [Fact]
    public async Task Owner_walks_every_page_opens_and_cancels_every_dialog_without_breaking()
    {
        var page = await app.NewAuthedPageAsync();

        foreach (var route in Routes)
        {
            output.WriteLine($"→ {route}");
            var response = await page.GotoAsync(route, new() { WaitUntil = WaitUntilState.NetworkIdle });
            response.Should().NotBeNull();
            response!.Status.Should().BeLessThan(500, $"GET {route} returned {response.Status} (prerender crash)");

            // Wait for the interactive circuit so button clicks actually dispatch.
            try { await page.WaitForFunctionAsync("() => window.Blazor !== undefined", null, new() { Timeout = 15000 }); }
            catch (TimeoutException) { /* a purely-static page is fine — nothing interactive to poke */ }

            await AssertHealthyAsync(page, route);
            // Some pages pop a dialog on load with no click (e.g. AI pages' "AI not configured — configure?"
            // prompt). Back out of anything already open before we start poking.
            await DismissOpenDialogsAsync(page, $"{route} (on load)");
            await PokeEveryDialogOpenerAsync(page, route);

            // Left nothing open behind us (MudBlazor can keep a hidden dialog node in the DOM, so count
            // only the visible ones).
            (await page.Locator(".mud-dialog:visible").CountAsync())
                .Should().Be(0, $"{route} left a dialog open after the walk");
            await AssertHealthyAsync(page, route);
        }
    }

    private async Task PokeEveryDialogOpenerAsync(IPage page, string route)
    {
        // Snapshot the page-level buttons (exclude anything already inside a dialog). Re-query by index each
        // pass because opening/closing a dialog re-renders the circuit and invalidates old handles.
        var buttonCount = await page.Locator("button:not(.mud-dialog button)").CountAsync();

        for (var i = 0; i < buttonCount; i++)
        {
            var button = page.Locator("button:not(.mud-dialog button)").Nth(i);

            string label;
            try
            {
                if (!await button.IsVisibleAsync() || !await button.IsEnabledAsync()) continue;
                label = ((await button.InnerTextAsync()) + " " + (await button.GetAttributeAsync("aria-label") ?? ""))
                    .Trim();
            }
            catch (PlaywrightException) { continue; /* re-rendered away — skip */ }

            if (string.IsNullOrWhiteSpace(label)) continue;
            if (Destructive.IsMatch(label)) continue;
            if (!DialogOpeners.IsMatch(label)) continue;

            output.WriteLine($"    · click «{label}»");
            // Park the mouse away from any control first: a lingering MudBlazor tooltip popover from a
            // previous hover otherwise sits over the next icon-button and intercepts the click. Then click,
            // treating an obscured/slow button as a skip (System.TimeoutException) — this walk asserts the
            // circuit never breaks, not that every button is reachable, so a non-crash miss is fine.
            try { await page.Mouse.MoveAsync(0, 0); } catch (PlaywrightException) { /* best effort */ }
            try { await button.ClickAsync(new() { Timeout = 5000 }); }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException) { continue; }

            // If a dialog appeared, back out of it; if not, the click was a no-op and we move on.
            var dialog = page.Locator(".mud-dialog").Last;
            var opened = false;
            try
            {
                await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 2500 });
                opened = true;
            }
            catch (TimeoutException) { /* not a dialog opener after all */ }

            if (opened) await CloseDialogAsync(page, dialog, label);

            await AssertHealthyAsync(page, $"{route} after «{label}»");
        }
    }

    private static async Task DismissOpenDialogsAsync(IPage page, string where)
    {
        for (var guard = 0; guard < 5; guard++)
        {
            var open = page.Locator(".mud-dialog:visible");
            if (await open.CountAsync() == 0) return;
            await CloseDialogAsync(page, open.Last, $"auto-open on {where}");
        }
    }

    private static async Task CloseDialogAsync(IPage page, ILocator dialog, string label)
    {
        // Try each cancel-style label, then fall back to Escape — whatever dismisses it.
        foreach (var close in CloseLabels)
        {
            var btn = dialog.Locator($"button:has-text('{close}')").First;
            if (await btn.CountAsync() == 0) continue;
            try
            {
                await btn.ClickAsync(new() { Timeout = 3000 });
                if (await Dismissed(dialog)) return;
            }
            catch (PlaywrightException) { /* try the next close affordance */ }
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await page.Keyboard.PressAsync("Escape");
            if (await Dismissed(dialog)) return;
        }

        throw new InvalidOperationException($"Could not cancel the dialog opened by «{label}».");
    }

    private static async Task<bool> Dismissed(ILocator dialog)
    {
        try
        {
            await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });
            return true;
        }
        catch (TimeoutException) { return false; }
    }

    private static async Task AssertHealthyAsync(IPage page, string where)
    {
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse($"{where}: Blazor error UI tripped (circuit broke)");
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync())
            .Should().BeFalse($"{where}: ErrorBoundary tripped (component threw)");
        (await page.Locator(".mud-appbar, header, nav").CountAsync())
            .Should().BeGreaterThan(0, $"{where}: app shell blanked out");
    }
}
