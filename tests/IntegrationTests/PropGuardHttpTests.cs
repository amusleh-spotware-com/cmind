using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the prop-guard rule endpoints over the real app + Postgres: create a rule against
// an owned account, list, update, delete, and reject an unowned account. (Coverage backfill.)
public class PropGuardHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@propguard.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:PropGuard", "true");
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<Guid> SeedAccountAsync(HttpClient client)
    {
        (await client.PostAsJsonAsync("/api/ctids/", new { Username = "pg-trader", Password = "p" }))
            .EnsureSuccessStatusCode();
        var cids = await (await client.GetAsync("/api/ctids/")).Content.ReadFromJsonAsync<JsonElement>();
        var cidId = cids.EnumerateArray().First().GetProperty("id").GetGuid();

        (await client.PostAsJsonAsync($"/api/ctids/{cidId}/accounts",
            new { AccountNumber = 7001234L, Broker = "Pepperstone", IsLive = false, Label = "demo" }))
            .EnsureSuccessStatusCode();
        var accounts = await (await client.GetAsync($"/api/ctids/{cidId}/accounts")).Content.ReadFromJsonAsync<JsonElement>();
        return accounts.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_list_update_and_delete_a_prop_rule()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var accountId = await SeedAccountAsync(client);

        var create = await client.PostAsJsonAsync("/api/prop/rules", new
        {
            TradingAccountId = accountId,
            Name = "daily guard",
            MaxConcurrentLiveInstances = 2,
            DailyLossLimit = 100.0,
            MaxDrawdownPercent = 5.0,
            AutoFlatten = true,
            Enabled = true,
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var list = await (await client.GetAsync("/api/prop/rules")).Content.ReadFromJsonAsync<JsonElement>();
        list.EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).Should().Contain(id);

        (await client.PutAsJsonAsync($"/api/prop/rules/{id}", new
        {
            Name = "tighter guard",
            MaxConcurrentLiveInstances = 1,
            DailyLossLimit = 50.0,
            MaxDrawdownPercent = 3.0,
            AutoFlatten = false,
            Enabled = false,
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.DeleteAsync($"/api/prop/rules/{id}")).StatusCode
            .Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Create_rejects_an_unowned_account()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.PostAsJsonAsync("/api/prop/rules",
            new { TradingAccountId = Guid.NewGuid(), Name = "guard", DailyLossLimit = 100.0 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
