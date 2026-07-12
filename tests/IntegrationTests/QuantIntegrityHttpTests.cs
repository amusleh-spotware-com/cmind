using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

public class QuantIntegrityHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@quant.local";
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
    public async Task Strong_edge_single_trial_scores_robust()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/quant/integrity",
            new { Returns = Series(250, 0.005, 0.001), Trials = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("verdict").GetString().Should().Be("Robust");
        body.GetProperty("probabilityOfBacktestOverfitting").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task No_edge_many_trials_scores_overfit()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/quant/integrity",
            new { Returns = Series(50, 0.0001, 0.02), Trials = 1000 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("verdict").GetString().Should().Be("Overfit");
        body.GetProperty("deflatedSharpe").GetDouble().Should().BeLessThan(0.90);
    }

    [Fact]
    public async Task Too_short_series_is_rejected()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/quant/integrity", new { Returns = new[] { 0.01 }, Trials = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Equity_curve_is_accepted()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/quant/integrity",
            new { Equity = new[] { 100.0, 101.0, 102.0, 103.0, 104.5, 106.0 }, Trials = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sharpe").GetDouble().Should().BeGreaterThan(0);
    }
}
