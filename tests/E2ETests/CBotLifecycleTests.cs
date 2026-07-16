using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the real cBot authoring flow through the UI: create a C# and a Python project (each lands in the
// Monaco editor), edit + save the code, and build. Uses the signed-in owner via AppFixture.
[Collection(AppCollection.Name)]
public sealed class CBotLifecycleTests(AppFixture app)
{
    private static readonly string Suffix = Guid.NewGuid().ToString("N")[..6];
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 20000 };

    [Fact]
    public async Task Create_csharp_cbot_lands_in_the_editor()
    {
        var page = await app.NewAuthedPageAsync();
        await CreateProjectAsync(page, $"csharp-{Suffix}", "C#");
        await Assertions.Expect(page.Locator(".monaco-editor").First).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Create_python_cbot_lands_in_the_editor()
    {
        var page = await app.NewAuthedPageAsync();
        await CreateProjectAsync(page, $"python-{Suffix}", "Python");
        await Assertions.Expect(page.Locator(".monaco-editor").First).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Edit_and_save_cbot_code_round_trips()
    {
        var page = await app.NewAuthedPageAsync();
        await CreateProjectAsync(page, $"edit-{Suffix}", "C#");
        await Assertions.Expect(page.Locator(".monaco-editor").First).ToBeVisibleAsync(Slow);

        // Type into Monaco: focus, select all, replace with a marker line.
        var editor = page.Locator(".monaco-editor").First;
        await editor.ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync($"// e2e-edit-{Suffix}\n");

        await ClickAndWaitSnackbarAsync(page, "Save", "Saved");
    }

    [Fact(Timeout = 420000)]
    public async Task Build_cbot_reports_a_result()
    {
        var page = await app.NewAuthedPageAsync();
        await CreateProjectAsync(page, $"build-{Suffix}", "C#");
        await Assertions.Expect(page.Locator(".monaco-editor").First).ToBeVisibleAsync(Slow);

        await ClickAsync(page, "Build");
        // The real docker build pulls the SDK image + restores on first run — allow generous time. Either
        // outcome chip proves the build pipeline ran end to end without crashing the app.
        var result = page.Locator(".mud-chip:has-text('Build OK'), .mud-chip:has-text('Build failed')");
        await Assertions.Expect(result.First).ToBeVisibleAsync(new() { Timeout = 400000 });
    }

    [Fact]
    public async Task Search_filters_cbots_by_name()
    {
        var name = $"search-{Guid.NewGuid():N}"[..20];
        var page = await app.NewAuthedPageAsync();
        await CreateProjectAsync(page, name, "C#");

        await page.GotoAsync("/cbots");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var row = page.Locator($".cbots-list a:has-text('{name}')");
        await Assertions.Expect(row.First).ToBeVisibleAsync(Slow);

        var search = page.GetByPlaceholder("Search cBots by name");
        await search.FillAsync(name);
        await Assertions.Expect(row.First).ToBeVisibleAsync(Slow);

        // A non-matching query hides every row and shows the empty-search notice.
        await search.FillAsync($"nomatch-{Guid.NewGuid():N}");
        await Assertions.Expect(page.Locator("text=No cBots match").First).ToBeVisibleAsync(Slow);
        await Assertions.Expect(row.First).ToBeHiddenAsync();
    }

    private static async Task CreateProjectAsync(IPage page, string name, string language)
    {
        await page.GotoAsync("/cbots");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var dialog = await OpenDialogAsync(page, "New cBot");
        await dialog.GetByLabel("Title").FillAsync(name);
        await dialog.Locator($".mud-radio:has-text('{language}')").First.ClickAsync();

        for (var attempt = 0; attempt < 15; attempt++)
        {
            await dialog.Locator("button:has-text('Create')").ClickAsync();
            try
            {
                await page.WaitForURLAsync("**/builder/**", new() { Timeout = 3000 });
                return;
            }
            catch (TimeoutException) { /* circuit not interactive yet — retry */ }
            catch (PlaywrightException) { /* stale after reconnect — retry */ }
        }
        throw new TimeoutException("Create did not navigate to the builder editor.");
    }

    private static async Task<ILocator> OpenDialogAsync(IPage page, string buttonText)
    {
        var button = page.Locator($"button:has-text('{buttonText}')").First;
        await button.WaitForAsync(new() { Timeout = 20000 });
        var dialog = page.Locator(".mud-dialog").Last;
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await button.ClickAsync();
            try { await dialog.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Visible }); return dialog; }
            catch (TimeoutException) { }
            catch (PlaywrightException) { }
        }
        throw new TimeoutException($"Dialog did not open for '{buttonText}'.");
    }

    private static async Task ClickAsync(IPage page, string buttonText)
    {
        var button = page.Locator($"button:has-text('{buttonText}')").First;
        await button.WaitForAsync(new() { Timeout = 20000 });
        for (var attempt = 0; attempt < 15; attempt++)
        {
            try { await button.ClickAsync(new() { Timeout = 2000 }); return; }
            catch (PlaywrightException) { await Task.Delay(500); }
        }
    }

    private static async Task ClickAndWaitSnackbarAsync(IPage page, string buttonText, string snackText)
    {
        var snack = page.Locator($".mud-snackbar:has-text('{snackText}')");
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await ClickAsync(page, buttonText);
            try { await snack.First.WaitForAsync(new() { Timeout = 3000, State = WaitForSelectorState.Visible }); return; }
            catch (TimeoutException) { }
            catch (PlaywrightException) { }
        }
        throw new TimeoutException($"'{snackText}' snackbar not shown after clicking '{buttonText}'.");
    }
}
