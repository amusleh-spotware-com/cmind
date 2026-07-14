using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

public class QuantPortfolioHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@portfolio.local";
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

    private static double[] Series(int n, double mean, double jitter) =>
        Enumerable.Range(0, n).Select(i => mean + (i % 2 == 0 ? jitter : -jitter)).ToArray();

    [Fact]
    public async Task Sizing_returns_a_recommendation()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/quant/sizing",
            new { Returns = Series(100, 0.002, 0.02), TargetVolatility = 0.10, KellyFraction = 0.5, LeverageCap = 3.0 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("recommendedFraction").GetDouble().Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task Sizing_from_an_equity_curve_returns_a_recommendation()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        // A rising, wobbling equity/balance curve (not raw returns) → the endpoint derives returns from it
        // and sizes to a positive exposure. Deterministic guard for the equity-mode path the UI drives.
        var equity = Enumerable.Range(0, 60).Select(i => 1000.0 + (i * 5) + (i % 2 == 0 ? 8 : -8)).ToArray();
        var response = await client.PostAsJsonAsync("/api/quant/sizing",
            new { Equity = equity, TargetVolatility = 0.10, KellyFraction = 0.5, LeverageCap = 3.0 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("recommendedFraction").GetDouble().Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task Portfolio_allocates_across_strategies()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/quant/portfolio", new
        {
            Strategies = new[] { Series(60, 0.001, 0.01), Series(60, 0.001, 0.02) },
            TargetVolatility = 0.10,
            LeverageCap = 3.0
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var weights = body.GetProperty("weights").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        weights.Should().HaveCount(2);
        weights[0].Should().BeGreaterThan(weights[1]); // lower-vol strategy weighted higher
    }

    [Fact]
    public async Task Portfolio_rejects_a_single_strategy()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/quant/portfolio",
            new { Strategies = new[] { Series(60, 0.001, 0.01) } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
