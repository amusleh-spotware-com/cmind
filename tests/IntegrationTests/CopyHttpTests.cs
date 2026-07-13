using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the copy-trading read endpoints + the feature gate over the real app + Postgres:
// profiles list, public marketplace, and the CopyTrading gate. (Coverage backfill — integration tier.)
public class CopyHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@copyhttp.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp(bool copyEnabled = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:CopyTrading", copyEnabled ? "true" : "false");
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Profiles_and_marketplace_read_endpoints_respond()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var profiles = await client.GetAsync("/api/copy/profiles");
        profiles.StatusCode.Should().Be(HttpStatusCode.OK);
        (await profiles.Content.ReadFromJsonAsync<JsonElement>()).ValueKind.Should().Be(JsonValueKind.Array);

        (await client.GetAsync("/api/copy/marketplace")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Copy_endpoints_are_gated_off_when_the_feature_is_disabled()
    {
        await using var app = CreateApp(copyEnabled: false);
        var client = await LoginAsync(app);

        (await client.GetAsync("/api/copy/profiles")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a disabled feature returns 404 for its endpoints");
    }
}
