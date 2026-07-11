using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// End-to-end (real app + Postgres, authed owner) for the copy features added this session: execution
// transparency, performance-fee report, notification feed, and the provider marketplace (publish, list,
// verified-live, unpublish). Exercised through the HTTP API the pages call.
[Collection(AppCollection.Name)]
public sealed class CopyFeaturesE2ETests(AppFixture app)
{
    private static readonly string Suffix = Guid.NewGuid().ToString("N")[..6];

    [Fact]
    public async Task Transparency_fees_notifications_and_marketplace_round_trip()
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        var cidId = await CreateCidAsync(api, $"feat-cid-{Suffix}");
        var master = await CreateAccountAsync(api, cidId, NextAccountNumber(40), "FeatMaster");
        var slave = await CreateAccountAsync(api, cidId, NextAccountNumber(41), "FeatSlave");

        var create = await api.PostAsync(U("/api/copy/profiles"), new()
        {
            DataObject = new { Name = $"feat-profile-{Suffix}", SourceAccountId = master, DestinationAccountIds = new[] { slave } }
        });
        Assert.True(create.Ok, $"create profile failed: {create.Status}");
        var profileId = (await ReadJsonAsync(create)).GetProperty("id").GetString()!;

        // Transparency read model — empty but well-formed.
        var transparency = await GetJsonAsync(api, $"/api/copy/profiles/{profileId}/transparency");
        Assert.Equal(0, transparency.GetProperty("summary").GetProperty("total").GetInt32());
        Assert.Equal(0, transparency.GetProperty("recent").GetArrayLength());

        // Fee report — empty but well-formed.
        var fees = await GetJsonAsync(api, $"/api/copy/profiles/{profileId}/fees");
        Assert.Equal(0d, fees.GetProperty("totalFee").GetDouble());
        Assert.Equal(0, fees.GetProperty("accruals").GetArrayLength());

        // Notification feed — well-formed.
        var notifications = await GetJsonAsync(api, "/api/copy/notifications");
        Assert.True(notifications.TryGetProperty("unacknowledged", out _));
        Assert.True(notifications.TryGetProperty("items", out _));

        // Marketplace: publish, then it appears (verified-live false — the source account is demo).
        var displayName = $"Strategy-{Suffix}";
        var publish = await api.PostAsync(U($"/api/copy/profiles/{profileId}/publish"), new()
        {
            DataObject = new { DisplayName = displayName, Description = "e2e strategy", PerformanceFeePercent = 20.0 }
        });
        Assert.True(publish.Ok, $"publish failed: {publish.Status}");
        var published = await ReadJsonAsync(publish);
        Assert.True(published.GetProperty("published").GetBoolean());
        Assert.False(published.GetProperty("verifiedLive").GetBoolean(), "a demo source account is not verified-live");

        var marketplace = await GetJsonAsync(api, "/api/copy/marketplace");
        Assert.Contains(marketplace.EnumerateArray(), l => l.GetProperty("displayName").GetString() == displayName);

        // Unpublish removes it from the marketplace.
        var unpublish = await api.DeleteAsync(U($"/api/copy/profiles/{profileId}/publish"));
        Assert.True(unpublish.Ok, $"unpublish failed: {unpublish.Status}");
        var afterUnpublish = await GetJsonAsync(api, "/api/copy/marketplace");
        Assert.DoesNotContain(afterUnpublish.EnumerateArray(), l => l.GetProperty("displayName").GetString() == displayName);
    }

    private string U(string path) => app.BaseUrl + path;
    private static long NextAccountNumber(int slot) => 800_000 + (Convert.ToInt64(Suffix, 16) % 50_000) * 100 + slot;

    private async Task<string> CreateCidAsync(IAPIRequestContext api, string username)
    {
        var r = await api.PostAsync(U("/api/ctids/"), new() { DataObject = new { Username = username, Password = "cid_password_123" } });
        Assert.True(r.Ok, $"create cid failed: {r.Status}");
        var cids = await GetJsonAsync(api, "/api/ctids/");
        return cids.EnumerateArray().First(c => c.GetProperty("username").GetString() == username).GetProperty("id").GetString()!;
    }

    private async Task<Guid> CreateAccountAsync(IAPIRequestContext api, string cidId, long number, string broker)
    {
        var r = await api.PostAsync(U($"/api/ctids/{cidId}/accounts"), new()
        {
            DataObject = new { AccountNumber = number, Broker = broker, IsLive = false, Label = (string?)null }
        });
        Assert.True(r.Ok, $"create account failed: {r.Status}");
        var accounts = await GetJsonAsync(api, "/api/accounts");
        return accounts.EnumerateArray().First(a => a.GetProperty("accountNumber").GetInt64() == number).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> GetJsonAsync(IAPIRequestContext api, string path)
    {
        var r = await api.GetAsync(U(path));
        Assert.True(r.Ok, $"GET {path} failed: {r.Status}");
        return JsonSerializer.Deserialize<JsonElement>(await r.TextAsync());
    }

    private static async Task<JsonElement> ReadJsonAsync(IAPIResponse response)
        => JsonSerializer.Deserialize<JsonElement>(await response.TextAsync());
}
