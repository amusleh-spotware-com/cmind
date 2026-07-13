using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

public class ComplianceFlowTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@compliance.local";
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
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { Email = Owner, Password, RememberMe = true });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    [Fact]
    public async Task Consent_gate_blocks_copy_creation_until_risk_disclosure_accepted()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var before = await client.PostAsJsonAsync("/api/copy/profiles",
            new { Name = "P1", SourceAccountId = Guid.NewGuid() });
        before.StatusCode.Should().Be(HttpStatusCode.OK, "no disclosure published yet, so nothing to consent to");

        var create = await client.PostAsJsonAsync("/api/compliance/documents",
            new { Type = 1, Version = 1, Body = "CFD risk disclosure" });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await client.PostAsync($"/api/compliance/documents/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var blocked = await client.PostAsJsonAsync("/api/copy/profiles",
            new { Name = "P2", SourceAccountId = Guid.NewGuid() });
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the published disclosure now requires consent");

        (await client.PostAsJsonAsync("/api/compliance/consent", new { Type = "RiskDisclosure" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var allowed = await client.PostAsJsonAsync("/api/copy/profiles",
            new { Name = "P3", SourceAccountId = Guid.NewGuid() });
        allowed.StatusCode.Should().Be(HttpStatusCode.OK, "consent recorded, so creation is unblocked");
    }

    [Fact]
    public async Task Export_returns_user_data_and_audit_chain_verifies()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var export = await client.GetFromJsonAsync<JsonElement>("/api/compliance/export");
        export.GetProperty("user").GetProperty("email").GetString().Should().Be(Owner);

        // D-11: the GDPR export must not leak the internal user UUID (its database primary key).
        export.GetProperty("user").TryGetProperty("id", out _)
            .Should().BeFalse("the user-facing GDPR export must not expose the internal user UUID");

        var audit = await client.GetFromJsonAsync<JsonElement>("/api/compliance/audit/verify");
        audit.GetProperty("intact").GetBoolean().Should().BeTrue();
    }
}
