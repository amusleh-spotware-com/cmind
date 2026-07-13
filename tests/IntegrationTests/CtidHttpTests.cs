using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the cTrader-ID account endpoints over the real app + Postgres: create a cID,
// list it, attach a trading account, update, and delete. (Coverage backfill — integration tier.)
public class CtidHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@ctid.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Create_cid_attach_account_update_and_delete()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.PostAsJsonAsync("/api/ctids/", new { Username = "trader1", Password = "cid-pass" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await (await client.GetAsync("/api/ctids/")).Content.ReadFromJsonAsync<JsonElement>();
        var mine = list.EnumerateArray().Single(c => c.GetProperty("username").GetString() == "trader1");
        var id = mine.GetProperty("id").GetGuid();

        // Attach a trading account (broker unrestricted by default -> no probe).
        (await client.PostAsJsonAsync($"/api/ctids/{id}/accounts",
            new { AccountNumber = 5001234L, Broker = "Pepperstone", IsLive = false, Label = "demo" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var accounts = await (await client.GetAsync($"/api/ctids/{id}/accounts")).Content.ReadFromJsonAsync<JsonElement>();
        accounts.EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("accountNumber").GetInt64().Should().Be(5001234L);

        (await client.PutAsJsonAsync($"/api/ctids/{id}", new { Username = "trader1-renamed", Password = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.DeleteAsync($"/api/ctids/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_and_delete_of_a_missing_cid_are_not_found()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.PutAsJsonAsync($"/api/ctids/{Guid.NewGuid()}", new { Username = "x", Password = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"/api/ctids/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
