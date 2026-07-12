using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

public class QuantHealthHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@health.local";
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
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password, RememberMe = true });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    private static double[] TwoPhase(int n, double m1, double j1, double m2, double j2)
    {
        var half = n / 2;
        return Enumerable.Range(0, n).Select(i =>
        {
            var (m, j) = i < half ? (m1, j1) : (m2, j2);
            return m + (i % 2 == 0 ? j : -j);
        }).ToArray();
    }

    [Fact]
    public async Task Reports_a_decayed_edge()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/quant/health",
            new { Returns = TwoPhase(40, 0.01, 0.002, 0.0, 0.02) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("health").GetString().Should().Be("Decayed");
    }

    [Fact]
    public async Task Reports_a_healthy_edge()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/quant/health",
            new { Returns = TwoPhase(40, 0.005, 0.001, 0.005, 0.001) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("health").GetString().Should().Be("Healthy");
    }
}
