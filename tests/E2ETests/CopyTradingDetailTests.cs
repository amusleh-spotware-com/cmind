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
    public async Task Deep_link_to_existing_profile_renders_full_page_editor()
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        var profileId = await CreateProfileAsync(api, $"deep-{Suffix}");

        await page.GotoAsync($"/copy-trading/{profileId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        // Editing a profile is a full page (like create), never a dialog.
        (await page.Locator(".mud-dialog").CountAsync())
            .Should().Be(0, "the copy-profile editor is a full page, not a dialog");
        (await page.GetByText($"deep-{Suffix}").First.IsVisibleAsync())
            .Should().BeTrue("the full-page editor must show the profile name in its header");
        await Assertions.Expect(page.Locator("[data-testid=copy-add-destination]")).ToBeVisibleAsync(Slow);
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse("the editor page must not trip the Blazor error UI");
    }

    [Fact]
    public async Task Money_management_dropdown_opens_on_the_full_page_without_crashing()
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        var profileId = await CreateProfileAsync(api, $"dd-{Suffix}");

        await page.GotoAsync($"/copy-trading/{profileId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        // The Money-management select (concrete enum items) opens and a value can be chosen inline on the
        // page — no dialog, no overlay trap.
        await page.Locator(".mud-select:has-text('Money management')").First.ClickAsync();
        var item = page.Locator(".mud-list-item").First;
        await item.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await item.ClickAsync();

        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse("selecting a sizing mode on the page must not trip the Blazor error UI");
    }

    [Fact]
    public async Task Selectors_show_broker_and_destination_mode_is_the_friendly_label()
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        var master = await SeedLinkedAccountAsync(api);
        var slave = await SeedLinkedAccountAsync(api);
        var name = $"broker-{Suffix}";

        // Create with the destination inline; it lands with the default sizing (Lot multiplier).
        var create = await api.PostAsync(U("/api/copy/profiles"), new()
        {
            DataObject = new { Name = name, SourceAccountId = master.Id, DestinationAccountIds = new[] { slave.Id } }
        });
        Assert.True(create.Ok, $"create profile failed: {create.Status}");
        var profileId = (await ReadJsonAsync(create)).GetProperty("id").GetString()!;

        await page.GotoAsync($"/copy-trading/{profileId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        // The source selector lists each account by its human number AND broker (never the raw GUID) — the
        // seed broker is "SeedBroker". Open the dropdown via its VISIBLE control (the testid lands on a
        // hidden MudSelect input that can't be clicked) — the source select is the first on the page.
        await page.Locator(".mud-select").First.ClickAsync();
        await Assertions.Expect(page.Locator(".mud-list-item:has-text('SeedBroker')").First).ToBeVisibleAsync(Slow);
        await page.Keyboard.PressAsync("Escape");

        // The destinations table renders the friendly money-management mode ("Lot multiplier"), like the
        // money-management selector — never the raw enum "LotMultiplier".
        await Assertions.Expect(page.Locator("[data-testid=copy-destinations]"))
            .ToContainTextAsync("Lot multiplier", new() { Timeout = 15000 });
    }

    private string U(string path) => app.BaseUrl + path;

    private async Task<(long Number, Guid Id)> SeedLinkedAccountAsync(IAPIRequestContext api)
    {
        var r = await api.PostAsync(U("/api/testseed/openapi-account"), new());
        Assert.True(r.Ok, $"seed openapi account failed: {r.Status}");
        var number = (await ReadJsonAsync(r)).GetProperty("accountNumber").GetInt64();
        var accounts = await GetJsonAsync(api, "/api/accounts");
        var id = accounts.EnumerateArray().First(a => a.GetProperty("accountNumber").GetInt64() == number)
            .GetProperty("id").GetGuid();
        return (number, id);
    }

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
