using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the owner feature-toggle endpoints over the real app + Postgres: snapshot lists
// flags, PUT flips one, an unknown flag 404s. (Coverage backfill — integration tier.)
public class FeatureHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@feature.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
        });

    private static async Task<HttpClient> OwnerAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password }))
            .EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Snapshot_lists_flags_and_put_toggles_one()
    {
        await using var app = CreateApp();
        var client = await OwnerAsync(app);

        var snapshot = await (await client.GetAsync("/api/features/")).Content.ReadFromJsonAsync<JsonElement>();
        snapshot.EnumerateArray().Should().NotBeEmpty();

        var put = await client.PutAsJsonAsync("/api/features/Mcp", new { Enabled = true });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("flag").GetString().Should().Be("Mcp");
        body.GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Unknown_flag_is_not_found()
    {
        await using var app = CreateApp();
        var client = await OwnerAsync(app);

        (await client.PutAsJsonAsync("/api/features/NotARealFlag", new { Enabled = true }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
