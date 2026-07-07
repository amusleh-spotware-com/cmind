using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class DialogTests(AppFixture app)
{
    private static readonly string Suffix = Guid.NewGuid().ToString("N")[..6];

    [Fact]
    public async Task Nodes_new_button_opens_dialog_and_creates_node()
    {
        var page = await app.NewAuthedPageAsync();
        await GotoAsync(page, "/nodes");

        var dialog = await OpenDialogAsync(page, "New Node");
        var inputs = dialog.Locator("input");
        await inputs.Nth(0).FillAsync($"E2E-Node-{Suffix}");
        await inputs.Nth(1).FillAsync("http://node-e2e:8080");
        await inputs.Nth(2).FillAsync("supersecret_shared_key_1234567890");
        await SubmitAsync(dialog, "Add node");

        await Assertions.Expect(page.GetByText($"E2E-Node-{Suffix}")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Users_new_button_opens_dialog_and_creates_user()
    {
        var page = await app.NewAuthedPageAsync();
        await GotoAsync(page, "/users");

        var email = $"user-{Suffix}@e2e.local";
        var dialog = await OpenDialogAsync(page, "New User");
        var inputs = dialog.Locator("input");
        await inputs.Nth(0).FillAsync(email);
        await inputs.Nth(1).FillAsync("Temp_Pass_123!");
        await SubmitAsync(dialog, "Create");

        await Assertions.Expect(page.GetByText(email)).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Mcp_new_button_opens_dialog_and_creates_key()
    {
        var page = await app.NewAuthedPageAsync();
        await GotoAsync(page, "/mcp");

        var label = $"key-{Suffix}";
        var dialog = await OpenDialogAsync(page, "New Key");
        await dialog.Locator("input").Nth(0).FillAsync(label);
        await SubmitAsync(dialog, "Create");

        await Assertions.Expect(page.GetByText(label)).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Accounts_new_button_creates_cid_and_trading_account()
    {
        var page = await app.NewAuthedPageAsync();
        await GotoAsync(page, "/accounts");

        var username = $"cid-{Suffix}";
        var cidDialog = await OpenDialogAsync(page, "New cID Account");
        var cidInputs = cidDialog.Locator("input");
        await cidInputs.Nth(0).FillAsync(username);
        await cidInputs.Nth(1).FillAsync("cid_password_123");
        await SubmitAsync(cidDialog, "Add");
        await Assertions.Expect(page.GetByText(username)).ToBeVisibleAsync(Slow);

        var accountNo = (100000 + Convert.ToInt32(Suffix, 16) % 900000).ToString();
        var taDialog = await OpenDialogAsync(page, "New Trading Account");
        var taInputs = taDialog.Locator("input");
        await taInputs.Nth(0).FillAsync(accountNo);
        await taInputs.Nth(1).FillAsync($"Broker-{Suffix}");
        await SubmitAsync(taDialog, "Add");

        await Assertions.Expect(page.GetByText($"Broker-{Suffix}")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task ParamSets_new_button_opens_dialog_and_creates_param_set()
    {
        var page = await app.NewAuthedPageAsync();

        // Seed a cBot through the real upload UI (no .algo validation, no Docker build needed).
        var cbotName = $"cbot-{Suffix}";
        await GotoAsync(page, "/cbots");
        await page.SetInputFilesAsync("#cbotFile", new FilePayload
        {
            Name = $"{cbotName}.algo",
            MimeType = "application/octet-stream",
            Buffer = "dummy-algo-content"u8.ToArray()
        });
        await Assertions.Expect(page.GetByText(cbotName)).ToBeVisibleAsync(Slow);

        await GotoAsync(page, "/paramsets");
        await page.Locator(".mud-select").First.ClickAsync();
        await page.Locator($".mud-list-item:has-text('{cbotName}')").First.ClickAsync();

        var paramName = $"pset-{Suffix}";
        var dialog = await OpenDialogAsync(page, "New Parameter Set");
        await dialog.Locator("input").First.FillAsync(paramName);
        await dialog.Locator("textarea").First.FillAsync("{\"Period\":14}");
        await SubmitAsync(dialog, "Save");

        await Assertions.Expect(page.GetByText(paramName)).ToBeVisibleAsync(Slow);
    }

    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    private static async Task GotoAsync(IPage page, string path)
    {
        await page.GotoAsync(path);
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
    }

    private static async Task<ILocator> OpenDialogAsync(IPage page, string buttonText)
    {
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
