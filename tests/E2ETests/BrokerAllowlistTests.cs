using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// App fixture with a restricted broker allowlist. The broker-probe algo is pointed at a missing path so
// manual-cID verification fails fast and CI-safe (no Docker, no live account): every add is rejected with
// the "couldn't verify" notification — which is exactly the restricted-deployment gate we want to prove
// end-to-end through the real UI.
public sealed class BrokerAllowlistFixture : AppFixture
{
    public const string AllowedBroker = "Pepperstone";

    protected override void ConfigureApp(ProcessStartInfo psi)
    {
        psi.Environment["App__Accounts__AllowedBrokers__0"] = AllowedBroker;
        psi.Environment["App__Accounts__BrokerProbeAlgoPath"] = "does-not-exist/broker-probe.algo";
    }
}

[CollectionDefinition(Name)]
public sealed class BrokerAllowlistCollection : ICollectionFixture<BrokerAllowlistFixture>
{
    public const string Name = "broker-allowlist";
}

[Collection(BrokerAllowlistCollection.Name)]
public sealed class BrokerAllowlistTests(BrokerAllowlistFixture app)
{
    private static readonly string Suffix = Guid.NewGuid().ToString("N")[..6];
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Restricted_deployment_rejects_a_manual_account_with_a_notification()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/accounts");
        await page.WaitForAppReadyAsync();

        // Create a cID to add accounts under.
        var cid = $"gate-cid-{Suffix}";
        var cidDialog = await OpenDialogAsync(page, "New cID Account");
        await cidDialog.Locator("input").Nth(0).FillAsync(cid);
        await cidDialog.Locator("input").Nth(1).FillAsync("cid_password_123");
        await SubmitAsync(cidDialog, "Add");
        await Assertions.Expect(page.GetByText(cid)).ToBeVisibleAsync(Slow);

        // Try to add a trading account: verification runs (probe algo missing) and fails, so the account
        // is rejected and a notification is shown.
        var broker = $"BlockedBroker-{Suffix}";
        var accountDialog = await OpenDialogAsync(page, "New Trading Account");
        await accountDialog.Locator("input").Nth(0).FillAsync("778899");
        await accountDialog.Locator("input").Nth(1).FillAsync(broker);
        await SubmitAsync(accountDialog, "Add");

        // The rejection notification appears...
        await Assertions.Expect(page.GetByText("Couldn't verify the account's broker"))
            .ToBeVisibleAsync(Slow);
        // ...and no account row was added for that broker.
        await Assertions.Expect(page.GetByText(broker)).Not.ToBeVisibleAsync();
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
