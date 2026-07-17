using System.Text.Json;
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
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // Enum options render as human labels, not raw enum names (money management default = Lot multiplier).
        await Assertions.Expect(page.GetByText("Lot multiplier").First).ToBeVisibleAsync(Slow);

        await page.GetByLabel("Profile name").FillAsync(profileName);
        // Source (master) is the first select on the page.
        await page.Locator(".mud-select").First.ClickAsync();
        await page.Locator($".mud-list-item:has-text('{master}')").First.ClickAsync();
        await page.Locator("[data-testid=copy-select-all]").ClickAsync();

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
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
    }
}
