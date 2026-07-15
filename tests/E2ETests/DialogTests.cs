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

        // Submitting opens a second dialog (the created key), so wait for that instead of the input closing.
        var keyDialog = page.Locator(".mud-dialog:has-text('MCP Key Created')");
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await dialog.Locator("button:has-text('Create')").ClickAsync();
            try
            {
                await keyDialog.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Visible });
                break;
            }
            catch (TimeoutException) { /* circuit not interactive yet — retry */ }
            catch (PlaywrightException) { /* stale locator after circuit reconnect — retry */ }
        }

        // New key is shown once in its own dialog with a copy button, not on the page.
        await Assertions.Expect(keyDialog.Locator("input").First).ToHaveValueAsync(
            new System.Text.RegularExpressions.Regex("^mcpk_"), new() { Timeout = 15000 });
        await Assertions.Expect(keyDialog.Locator("button:has-text('Copy')")).ToBeVisibleAsync(Slow);
        await SubmitAsync(keyDialog, "Close");

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
        // Type column shows Demo for a non-live account (the Live toggle defaults off).
        var accountRow = page.Locator($"tr:has-text('Broker-{Suffix}')");
        await Assertions.Expect(accountRow.GetByText("Demo")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Accounts_live_toggle_marks_account_type_live()
    {
        var page = await app.NewAuthedPageAsync();
        await GotoAsync(page, "/accounts");

        var username = $"cidlive-{Suffix}";
        var cidDialog = await OpenDialogAsync(page, "New cID Account");
        var cidInputs = cidDialog.Locator("input");
        await cidInputs.Nth(0).FillAsync(username);
        await cidInputs.Nth(1).FillAsync("cid_password_123");
        await SubmitAsync(cidDialog, "Add");
        await Assertions.Expect(page.GetByText(username)).ToBeVisibleAsync(Slow);

        var broker = $"RealBroker-{Suffix}";
        var taDialog = await OpenDialogAsync(page, "New Trading Account");
        var taInputs = taDialog.Locator("input");
        await taInputs.Nth(0).FillAsync((200000 + Convert.ToInt32(Suffix, 16) % 700000).ToString());
        await taInputs.Nth(1).FillAsync(broker);
        // Flip the Live toggle on before submitting.
        await taDialog.Locator("label.mud-switch").ClickAsync();
        await SubmitAsync(taDialog, "Add");

        await Assertions.Expect(page.GetByText(broker)).ToBeVisibleAsync(Slow);
        var accountRow = page.Locator($"tr:has-text('{broker}')");
        await Assertions.Expect(accountRow.GetByText("Live", new() { Exact = true })).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task ParamSets_new_button_opens_dialog_and_creates_param_set()
    {
        var page = await app.NewAuthedPageAsync();

        // Seed a cBot through the real upload UI (no .algo validation, no Docker build needed).
        var cbotName = $"cbot-{Suffix}";
        await GotoAsync(page, "/cbots");
        // Retry the upload until the row appears: a file-input change fired before the Blazor circuit is
        // interactive is silently dropped, so a single-shot upload flakes under parallel-boot CI load.
        var cbotRow = page.GetByText(cbotName).First;
        await page.RunUntilVisibleAsync(() => page.SetInputFilesAsync("#cbotFile", new FilePayload
        {
            Name = $"{cbotName}.algo",
            MimeType = "application/octet-stream",
            Buffer = "dummy-algo-content"u8.ToArray()
        }), cbotRow);
        await Assertions.Expect(cbotRow).ToBeVisibleAsync(Slow);

        // Parameter sets now live per-cBot: open them from the cBot row's params button.
        var paramsButton = page.Locator($"tr:has-text('{cbotName}') [data-testid=paramsets-btn]").First;
        var paramsDialog = page.Locator(".mud-dialog:has-text('Parameter Sets')");
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await paramsButton.ClickAsync();
            try
            {
                await paramsDialog.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Visible });
                break;
            }
            catch (TimeoutException) { /* circuit not interactive yet — retry */ }
            catch (PlaywrightException) { /* stale after reconnect — retry */ }
        }

        // The editor is a second dialog stacked over the params list, so wait on its Save button
        // (only the editor has one) rather than "any dialog visible".
        var paramName = $"pset-{Suffix}";
        var newBtn = paramsDialog.Locator("[data-testid=new-paramset]");
        var save = page.Locator(".mud-dialog button:has-text('Save')");
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await newBtn.ClickAsync();
            try
            {
                await save.First.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Visible });
                break;
            }
            catch (TimeoutException) { /* circuit not interactive yet — retry */ }
            catch (PlaywrightException) { /* stale after reconnect — retry */ }
        }
        await page.GetByLabel("Name").FillAsync(paramName);
        await page.Locator(".mud-dialog textarea").Last.FillAsync("{\"Period\":14}");
        await save.First.ClickAsync();

        await Assertions.Expect(page.GetByText(paramName)).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Run_new_button_opens_dialog_with_fields()
    {
        var page = await app.NewAuthedPageAsync();
        await GotoAsync(page, "/run");

        var dialog = await OpenDialogAsync(page, "Run New cBot");
        await Assertions.Expect(dialog.GetByLabel("Symbol")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(dialog.Locator("button:has-text('Start')")).ToBeVisibleAsync(Slow);
        await SubmitAsync(dialog, "Cancel");
    }

    [Fact]
    public async Task Backtest_new_button_opens_dialog_with_fields()
    {
        var page = await app.NewAuthedPageAsync();
        await GotoAsync(page, "/backtest");

        var dialog = await OpenDialogAsync(page, "Backtest New cBot");
        await Assertions.Expect(dialog.GetByLabel("Symbol")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(dialog.Locator("button:has-text('Start backtest')")).ToBeVisibleAsync(Slow);
        await SubmitAsync(dialog, "Cancel");
    }

    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

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
