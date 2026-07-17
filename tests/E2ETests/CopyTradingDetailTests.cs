using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// C-05: /copy-trading/{id} deep-links a single copy profile. An existing id opens the profile detail
// dialog; a missing/unauthorized id renders an actionable notice with a back link, never a 404 or a
// blank shell.
// C-06: inside the CopyProfileDialog, opening a MudSelect dropdown must not trap focus/pointer so that
// the Close (Cancel) button becomes unclickable — the dialog stays dismissible at all times.
[Collection(AppCollection.Name)]
public sealed class CopyTradingDetailTests(AppFixture app)
{
    private static readonly string Suffix = Guid.NewGuid().ToString("N")[..6];
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Deep_link_to_missing_profile_shows_notice_and_does_not_crash()
    {
        var page = await app.NewAuthedPageAsync();
        var missingId = "00000000-0000-0000-0000-0000000000cc";
        await page.GotoAsync($"/copy-trading/{missingId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(page.Locator("[data-testid=copy-not-found]")).ToBeVisibleAsync(Slow);
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse("a missing copy profile must not trip the Blazor error UI");
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync())
            .Should().BeFalse("a missing copy profile must not trip the ErrorBoundary");
    }

    [Fact]
    public async Task Deep_link_to_existing_profile_opens_detail_dialog()
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        var profileId = await CreateProfileAsync(api, $"deep-{Suffix}");

        await page.GotoAsync($"/copy-trading/{profileId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var dialog = page.Locator(".mud-dialog").Last;
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        (await dialog.GetByText($"deep-{Suffix}").First.IsVisibleAsync())
            .Should().BeTrue("the deep-linked profile's detail dialog must show its name");
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse("the deep-link page must not trip the Blazor error UI");
    }

    [Fact]
    public async Task Cancel_is_reachable_after_opening_a_dropdown_in_the_profile_dialog()
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        var profileId = await CreateProfileAsync(api, $"cancel-{Suffix}");

        await page.GotoAsync($"/copy-trading/{profileId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var dialog = page.Locator(".mud-dialog").Last;
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        // Open a MudSelect dropdown inside the dialog (the Money-management select, which has concrete
        // items). Click the select's input control by its surrounding label text.
        await dialog.Locator(".mud-select:has-text('Money management')").First.ClickAsync();
        await page.Locator(".mud-list-item").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        // With the dropdown open, the dialog's Close button must still dismiss the dialog. MudBlazor's
        // click-away overlay closes the dropdown first; a second click reaches Close — but Close must
        // never be permanently trapped under the overlay.
        var close = dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true });
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await close.ClickAsync(new() { Force = true });
            try
            {
                await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });
                break;
            }
            catch (TimeoutException) { /* first click dismissed the dropdown overlay — retry Close */ }
        }

        (await page.Locator(".mud-dialog").IsVisibleAsync())
            .Should().BeFalse("Close must remain reachable after a dropdown was opened (C-06)");
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse("dismissing the dialog must not trip the Blazor error UI");
    }

    private string U(string path) => app.BaseUrl + path;

    private async Task<string> CreateProfileAsync(IAPIRequestContext api, string name)
    {
        var cidId = await CreateCidAsync(api, $"cid-{name}");
        var master = await CreateAccountAsync(api, cidId, NextAccountNumber(80), "Master");
        var slave = await CreateAccountAsync(api, cidId, NextAccountNumber(81), "Slave");

        var create = await api.PostAsync(U("/api/copy/profiles"), new()
        {
            DataObject = new { Name = name, SourceAccountId = master, DestinationAccountIds = new[] { slave } }
        });
        Assert.True(create.Ok, $"create profile failed: {create.Status}");
        return (await ReadJsonAsync(create)).GetProperty("id").GetString()!;
    }

    private static long NextAccountNumber(int slot) => 800_000 + (Convert.ToInt64(Suffix, 16) % 50_000) * 100 + slot;

    private async Task<string> CreateCidAsync(IAPIRequestContext api, string username)
    {
        var r = await api.PostAsync(U("/api/ctids/"), new() { DataObject = new { Username = username, Password = "cid_password_123" } });
        Assert.True(r.Ok, $"create cid failed: {r.Status}");
        var cids = await GetJsonAsync(api, "/api/ctids/");
        return cids.EnumerateArray().First(c => c.GetProperty("username").GetString() == username)
            .GetProperty("id").GetString()!;
    }

    private async Task<Guid> CreateAccountAsync(IAPIRequestContext api, string cidId, long number, string broker)
    {
        var r = await api.PostAsync(U($"/api/ctids/{cidId}/accounts"), new()
        {
            DataObject = new { AccountNumber = number, Broker = broker, IsLive = false, Label = (string?)null }
        });
        Assert.True(r.Ok, $"create account failed: {r.Status}");
        var accounts = await GetJsonAsync(api, "/api/accounts");
        return accounts.EnumerateArray().First(a => a.GetProperty("accountNumber").GetInt64() == number)
            .GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> GetJsonAsync(IAPIRequestContext api, string path)
    {
        var r = await api.GetAsync(U(path));
        Assert.True(r.Ok, $"GET {path} failed: {r.Status}");
        return await ReadJsonAsync(r);
    }

    private static async Task<JsonElement> ReadJsonAsync(IAPIResponse response)
    {
        var text = await response.TextAsync();
        return JsonSerializer.Deserialize<JsonElement>(text);
    }
}
