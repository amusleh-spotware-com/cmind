using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Domain;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class CopyTradingTests(AppFixture app)
{
    private static readonly string Suffix = Guid.NewGuid().ToString("N")[..6];
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    // ---------------- UI: profile creation is a full page ----------------

    [Fact]
    public async Task New_profile_page_creates_profile_with_symbol_map_from_proper_controls()
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        // Copy trading lists only Open-API-linked accounts, so seed two of them (no live broker needed).
        var master = await SeedOpenApiAccountAsync(api);
        var slave = await SeedOpenApiAccountAsync(api);

        await GotoAsync(page, "/copy-trading");
        var profileName = $"ui-profile-{Suffix}";

        // The New Profile button navigates to the full-page create form (not a dialog).
        await page.Locator("[data-testid=copy-new-profile]").ClickAsync();
        await page.WaitForURLAsync("**/copy-trading/new");
        await page.WaitForAppReadyAsync();

        // Enum options render as human labels, not raw enum names (money management default = Lot multiplier).
        await Assertions.Expect(page.GetByText("Lot multiplier").First).ToBeVisibleAsync(Slow);

        await page.GetByLabel("Profile name").FillAsync(profileName);
        // Source (master) is the first select on the page.
        await page.Locator(".mud-select").First.ClickAsync();
        await page.Locator($".mud-list-item:has-text('{master}')").First.ClickAsync();
        // Pick THIS test's own destination explicitly — never select-all. Other tests in this collection
        // seed extra Open-API accounts into the shared user; select-all would attach them too and break the
        // single-destination assertions below (CI flake: "Sequence contains more than one element").
        await page.Locator("[data-testid=copy-destinations-select]").ClickAsync();
        await page.Locator($".mud-list-item:has-text('{slave}')").First.ClickAsync();
        await page.Keyboard.PressAsync("Escape");

        // Symbol map via the proper add/remove controls (no comma-separated blob).
        await page.Locator("[data-testid=symbol-map-source]").FillAsync("EURUSD");
        await page.Locator("[data-testid=symbol-map-destination]").FillAsync("EUR.USD");
        await page.Locator("[data-testid=symbol-map-add]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid=symbol-map-rows]"))
            .ToContainTextAsync("EUR.USD", new() { Timeout = 15000 });

        await page.Locator("[data-testid=copy-create]").ClickAsync();
        await page.WaitForURLAsync("**/copy-trading");

        var row = page.Locator($"tr:has-text('{profileName}')");
        await Assertions.Expect(row).ToBeVisibleAsync(Slow);
        await Assertions.Expect(row.GetByText("Draft")).ToBeVisibleAsync(Slow);

        // The map entered through the UI controls reached the destination.
        var profiles = await GetJsonAsync(api, "/api/copy/profiles");
        var id = profiles.EnumerateArray().First(p => p.GetProperty("name").GetString() == profileName)
            .GetProperty("id").GetString()!;
        var detail = await GetJsonAsync(api, $"/api/copy/profiles/{id}");
        var dest = detail.GetProperty("destinations").EnumerateArray().Single();
        var map = dest.GetProperty("symbolMaps").EnumerateArray().Single();
        Assert.Equal("EURUSD", map.GetProperty("source").GetString());
    }

    [Fact]
    public async Task New_profile_page_offers_import_export_controls()
    {
        var page = await app.NewAuthedPageAsync();
        await GotoAsync(page, "/copy-trading/new");

        // Import/export of the whole settings block and of the symbol map (CSV) are offered as controls.
        await Assertions.Expect(page.Locator("[data-testid=copy-export-settings]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=copy-import-settings]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=symbol-map-import]")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Profile_row_edit_button_opens_dialog_and_is_disabled_while_running()
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        var cidId = await CreateCidAsync(api, $"edit-cid-{Suffix}");
        var master = await CreateAccountAsync(api, cidId, NextAccountNumber(40), "EditMaster");
        var slave = await CreateAccountAsync(api, cidId, NextAccountNumber(41), "EditSlave");
        var name = $"edit-ui-{Suffix}";

        var create = await api.PostAsync(U("/api/copy/profiles"), new()
        {
            DataObject = new { Name = name, SourceAccountId = master, DestinationAccountIds = new[] { slave } }
        });
        Assert.True(create.Ok, $"create profile failed: {create.Status}");
        var profileId = (await ReadJsonAsync(create)).GetProperty("id").GetString()!;

        await GotoAsync(page, "/copy-trading");
        var row = page.Locator($"tr:has-text('{name}')");
        await Assertions.Expect(row).ToBeVisibleAsync(Slow);

        // Draft: the edit control navigates to the full-page editor (like create), not a dialog.
        var edit = row.Locator("[data-testid=copy-edit]");
        await Assertions.Expect(edit).ToBeEnabledAsync(new() { Timeout = 15000 });
        await edit.ClickAsync();
        await page.WaitForURLAsync($"**/copy-trading/{profileId}");
        await page.WaitForAppReadyAsync();
        Assert.Equal(0, await page.Locator(".mud-dialog").CountAsync());
        await Assertions.Expect(page.Locator("[data-testid=copy-add-destination]")).ToBeVisibleAsync(Slow);
        Assert.True(await page.GetByText(name).First.IsVisibleAsync(),
            "the editor page must show the profile name");

        // Running: editing is invalid, so the control is disabled (mandate 11: state-correct controls).
        Assert.Equal("Running", await ActAsync(api, profileId, "start"));
        await GotoAsync(page, "/copy-trading");
        await Assertions.Expect(page.Locator($"tr:has-text('{name}')").Locator("[data-testid=copy-edit]"))
            .ToBeDisabledAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task Edit_page_can_change_source_master_and_rejects_a_destination_as_source()
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        // Copy lists only Open-API-linked accounts; seed three (no live broker needed).
        var master1 = await SeedOpenApiAccountAsync(api);
        var master2 = await SeedOpenApiAccountAsync(api);
        var slave = await SeedOpenApiAccountAsync(api);
        var master1Id = await AccountIdAsync(api, master1);
        var master2Id = await AccountIdAsync(api, master2);
        var slaveId = await AccountIdAsync(api, slave);

        var create = await api.PostAsync(U("/api/copy/profiles"), new()
        {
            DataObject = new { Name = $"src-{Suffix}", SourceAccountId = master1Id, DestinationAccountIds = new[] { slaveId } }
        });
        Assert.True(create.Ok, $"create profile failed: {create.Status}");
        var profileId = (await ReadJsonAsync(create)).GetProperty("id").GetString()!;

        // The source (master) can be changed to another account.
        var change = await api.PutAsync(U($"/api/copy/profiles/{profileId}/source"),
            new() { DataObject = new { SourceAccountId = master2Id } });
        Assert.Equal(200, change.Status);
        var detail = await GetJsonAsync(api, $"/api/copy/profiles/{profileId}");
        Assert.Equal(master2Id.ToString(), detail.GetProperty("sourceAccountId").GetString());

        // A current destination can never become the source (domain invariant → 400).
        var invalid = await api.PutAsync(U($"/api/copy/profiles/{profileId}/source"),
            new() { DataObject = new { SourceAccountId = slaveId } });
        Assert.Equal(400, invalid.Status);

        // The full-page editor exposes the source (master) selector, reflecting the changed master.
        // NB: MudSelect's data-testid lands on a hidden <input> — assert its VALUE, never its visibility.
        await GotoAsync(page, $"/copy-trading/{profileId}");
        await Assertions.Expect(page.Locator("[data-testid=copy-source-select]"))
            .ToHaveValueAsync(master2.ToString(), new() { Timeout = 15000 });

        // The browser tab title shows the profile name (polled — the title updates once the profile loads).
        await Assertions.Expect(page).ToHaveTitleAsync(
            new Regex(Regex.Escape($"src-{Suffix}")), new() { Timeout = 15000 });

        // The destination picker excludes the current source and any already-added destination — only free
        // accounts are offered (master1 was freed when the source moved to master2). Open the select by its
        // visible control (the testid is on the hidden input, which can't be clicked).
        await page.Locator(".mud-select").Filter(new() { HasTextString = "Destination (slave) accounts" })
            .First.ClickAsync();
        await page.Locator(".mud-list-item").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.True(await page.Locator($".mud-list-item:has-text('{master1}')").CountAsync() > 0, "a free account must be offered");
        Assert.Equal(0, await page.Locator($".mud-list-item:has-text('{slave}')").CountAsync());   // already a destination
        Assert.Equal(0, await page.Locator($".mud-list-item:has-text('{master2}')").CountAsync());  // current source
    }

    // ---------------- Non-UI (API): multi-slave, options, lifecycle ----------------

    [Fact]
    public async Task Api_creates_profile_with_multiple_slave_accounts()
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        var cidId = await CreateCidAsync(api, $"api-cid-{Suffix}");
        var master = await CreateAccountAsync(api, cidId, NextAccountNumber(20), "ApiMaster");
        var slave1 = await CreateAccountAsync(api, cidId, NextAccountNumber(21), "ApiSlave1");
        var slave2 = await CreateAccountAsync(api, cidId, NextAccountNumber(22), "ApiSlave2");

        var create = await api.PostAsync(U("/api/copy/profiles"), new()
        {
            DataObject = new
            {
                Name = $"api-multi-{Suffix}",
                SourceAccountId = master,
                DestinationAccountIds = new[] { slave1, slave2 }
            }
        });
        Assert.True(create.Ok, $"create profile failed: {create.Status}");
        var profileId = (await ReadJsonAsync(create)).GetProperty("id").GetString()!;

        var detail = await GetJsonAsync(api, $"/api/copy/profiles/{profileId}");
        Assert.Equal(2, detail.GetProperty("destinations").GetArrayLength());
    }

    [Fact]
    public async Task Api_destination_options_round_trip_and_lifecycle_transitions()
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        var cidId = await CreateCidAsync(api, $"opt-cid-{Suffix}");
        var master = await CreateAccountAsync(api, cidId, NextAccountNumber(30), "OptMaster");
        var slave = await CreateAccountAsync(api, cidId, NextAccountNumber(31), "OptSlave");

        var create = await api.PostAsync(U("/api/copy/profiles"), new()
        {
            DataObject = new { Name = $"api-opt-{Suffix}", SourceAccountId = master }
        });
        Assert.True(create.Ok);
        var profileId = (await ReadJsonAsync(create)).GetProperty("id").GetString()!;

        // Add one destination exercising every copy option.
        var addDest = await api.PostAsync(U($"/api/copy/profiles/{profileId}/destinations"), new()
        {
            DataObject = new
            {
                DestinationAccountId = slave,
                Mode = (int)MoneyManagementMode.FixedRiskPercent,
                Parameter = 2.5,
                SlippagePips = 1.5,
                MaxDelaySeconds = 30,
                Reverse = true,
                CopyStopLoss = true,
                CopyTakeProfit = false,
                Direction = (int)CopyDirectionFilter.LongOnly,
                MinLot = 0.01,
                MaxLot = 5.0,
                ForceMinLot = true,
                MaxDrawdownPercent = 20.0,
                DailyLossLimit = 500.0,
                SymbolFilterMode = (int)SymbolFilterMode.Whitelist,
                SymbolFilters = new[] { "EURUSD", "GBPUSD" },
                SymbolMap = new[] { new { Source = "EURUSD", Destination = "EURUSD.x" } },
                OrderTypes = (int)(CopyOrderTypes.Limit | CopyOrderTypes.Stop),
                CopyPendingExpiry = false,
                CopyMasterSlippage = false
            }
        });
        Assert.True(addDest.Ok, $"add destination failed: {addDest.Status}");

        var detail = await GetJsonAsync(api, $"/api/copy/profiles/{profileId}");
        var dest = detail.GetProperty("destinations").EnumerateArray().Single();
        Assert.Equal("FixedRiskPercent", dest.GetProperty("mode").GetString());
        Assert.Equal(2.5, dest.GetProperty("riskParameter").GetDouble());
        Assert.True(dest.GetProperty("reverse").GetBoolean());
        Assert.False(dest.GetProperty("copyTakeProfit").GetBoolean());
        Assert.Equal("LongOnly", dest.GetProperty("direction").GetString());
        Assert.True(dest.GetProperty("forceMinLot").GetBoolean());
        Assert.Equal(20.0, dest.GetProperty("maxDrawdownPercent").GetDouble());
        Assert.Equal("Whitelist", dest.GetProperty("symbolFilterMode").GetString());
        Assert.Equal(2, dest.GetProperty("symbolFilters").GetArrayLength());
        var map = dest.GetProperty("symbolMaps").EnumerateArray().Single();
        Assert.Equal("EURUSD", map.GetProperty("source").GetString());
        Assert.Equal("EURUSD.X", map.GetProperty("destination").GetString());
        Assert.Equal("Limit, Stop", dest.GetProperty("orderTypes").GetString());
        Assert.False(dest.GetProperty("copyPendingExpiry").GetBoolean());
        Assert.False(dest.GetProperty("copyMasterSlippage").GetBoolean());

        // Lifecycle: Draft -> Running -> Paused -> Running -> Stopped.
        Assert.Equal("Running", await ActAsync(api, profileId, "start"));
        Assert.Equal("Paused", await ActAsync(api, profileId, "pause"));
        Assert.Equal("Running", await ActAsync(api, profileId, "start"));
        Assert.Equal("Stopped", await ActAsync(api, profileId, "stop"));
    }

    // ---------------- helpers ----------------

    private string U(string path) => app.BaseUrl + path;

    private static long NextAccountNumber(int slot) => 700_000 + (Convert.ToInt64(Suffix, 16) % 50_000) * 100 + slot;

    private async Task<long> SeedOpenApiAccountAsync(IAPIRequestContext api)
    {
        var r = await api.PostAsync(U("/api/testseed/openapi-account"), new());
        Assert.True(r.Ok, $"seed openapi account failed: {r.Status}");
        return (await ReadJsonAsync(r)).GetProperty("accountNumber").GetInt64();
    }

    private async Task<Guid> AccountIdAsync(IAPIRequestContext api, long number)
    {
        var accounts = await GetJsonAsync(api, "/api/accounts");
        return accounts.EnumerateArray().First(a => a.GetProperty("accountNumber").GetInt64() == number)
            .GetProperty("id").GetGuid();
    }

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

    private async Task<string> ActAsync(IAPIRequestContext api, string profileId, string action)
    {
        var r = await api.PostAsync(U($"/api/copy/profiles/{profileId}/{action}"), new());
        Assert.True(r.Ok, $"action {action} failed: {r.Status}");
        return (await ReadJsonAsync(r)).GetProperty("status").GetString()!;
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

    private static async Task GotoAsync(IPage page, string path)
    {
        await page.GotoAsync(path);
        await page.WaitForAppReadyAsync();
    }
}
