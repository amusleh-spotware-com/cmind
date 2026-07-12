using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

public class UsageEndpointsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@usage.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
        });

    [Fact]
    public async Task Owner_gets_a_usage_summary()
    {
        await using var app = CreateApp();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password, RememberMe = true });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Add("Cookie", login.Headers.GetValues("Set-Cookie").First().Split(';')[0]);

        var usage = await client.GetFromJsonAsync<JsonElement>("/api/usage");

        usage.GetProperty("users").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        usage.GetProperty("nodes").TryGetProperty("online", out _).Should().BeTrue();
        usage.GetProperty("instances").TryGetProperty("backtestsRunning", out _).Should().BeTrue();
        usage.GetProperty("cbots").TryGetProperty("total", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        await using var app = CreateApp();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/usage");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Redirect, HttpStatusCode.Forbidden);
    }
}
