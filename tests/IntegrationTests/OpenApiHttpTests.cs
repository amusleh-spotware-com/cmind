using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the per-user Open API application endpoints over the real app + Postgres: save,
// read back, delete, secret-required validation, and the feature gate. (Coverage backfill.)
public class OpenApiHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@openapi.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp(bool enabled = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:OpenApi", enabled ? "true" : "false");
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Save_read_back_and_delete_the_open_api_application()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var before = await (await client.GetAsync("/api/openapi/application")).Content.ReadFromJsonAsync<JsonElement>();
        before.GetProperty("configured").GetBoolean().Should().BeFalse();

        (await client.PutAsJsonAsync("/api/openapi/application",
            new { Name = "My App", ClientId = "client-123", ClientSecret = "the-secret" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await (await client.GetAsync("/api/openapi/application")).Content.ReadFromJsonAsync<JsonElement>();
        after.GetProperty("configured").GetBoolean().Should().BeTrue();
        after.GetProperty("clientId").GetString().Should().Be("client-123");
        after.GetProperty("name").GetString().Should().Be("My App");

        (await client.DeleteAsync("/api/openapi/application")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await client.GetAsync("/api/openapi/application")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("configured").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Creating_an_application_requires_a_client_secret()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.PutAsJsonAsync("/api/openapi/application",
            new { Name = "No Secret", ClientId = "client-x", ClientSecret = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Endpoints_are_gated_off_when_the_feature_is_disabled()
    {
        await using var app = CreateApp(enabled: false);
        var client = await LoginAsync(app);

        (await client.GetAsync("/api/openapi/application")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
