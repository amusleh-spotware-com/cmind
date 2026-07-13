using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the MCP API-key endpoints over the real app + Postgres: create issues a token,
// it appears in the list, revoke removes it. (Coverage backfill — integration tier.)
public class McpKeyHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@mcpkey.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:Mcp", "true");
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password }))
            .EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Create_list_and_revoke_an_mcp_key()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var created = await client.PostAsJsonAsync("/api/mcp-keys/", new { Label = "CI key" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        token.Should().StartWith("mcpk_");

        var list = await (await client.GetAsync("/api/mcp-keys/")).Content.ReadFromJsonAsync<JsonElement>();
        var mine = list.EnumerateArray().Single(k => k.GetProperty("label").GetString() == "CI key");
        var id = mine.GetProperty("id").GetGuid();

        (await client.DeleteAsync($"/api/mcp-keys/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await (await client.GetAsync("/api/mcp-keys/")).Content.ReadFromJsonAsync<JsonElement>();
        after.EnumerateArray().Select(k => k.GetProperty("id").GetGuid()).Should().NotContain(id,
            "a revoked key is no longer listed");
    }

    [Fact]
    public async Task Mcp_keys_require_authentication()
    {
        await using var app = CreateApp();
        var anon = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await anon.GetAsync("/api/mcp-keys/")).StatusCode
            .Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Redirect, HttpStatusCode.Found);
    }
}
