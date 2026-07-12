using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

public class QuantRegimesHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@regimes.local";
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

    [Fact]
    public async Task Labels_regimes_and_reports_hurst()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var returns = Enumerable.Range(0, 60).Select(i =>
        {
            var jitter = i < 30 ? 0.0005 : 0.02;
            return i % 2 == 0 ? jitter : -jitter;
        }).ToArray();

        var response = await client.PostAsJsonAsync("/api/quant/regimes", new { Returns = returns, Window = 6 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("hurstExponent", out _).Should().BeTrue();
        var regimes = body.GetProperty("byRegime").EnumerateArray().Select(e => e.GetProperty("regime").GetString()).ToArray();
        regimes.Should().Contain("Calm").And.Contain("Turbulent");
    }
}
