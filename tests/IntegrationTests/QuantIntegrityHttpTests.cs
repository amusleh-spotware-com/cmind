using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

public class QuantIntegrityHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@quant.local";
    private const string Password = "Owner_Pass_123!";
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    // A rising equity curve (parsed by ContainerCommandHelpers.ParseEquityCurve) so a completed backtest can
    // be scored end to end from its stored report.
    private const string EquityReportJson =
        """
        {"equityHistory":[
          {"time":"2024-01-01T00:00:00Z","equity":10000.0},
          {"time":"2024-02-01T00:00:00Z","equity":10120.0},
          {"time":"2024-03-01T00:00:00Z","equity":10080.0},
          {"time":"2024-04-01T00:00:00Z","equity":10210.0},
          {"time":"2024-05-01T00:00:00Z","equity":10190.0},
          {"time":"2024-06-01T00:00:00Z","equity":10320.0},
          {"time":"2024-07-01T00:00:00Z","equity":10280.0},
          {"time":"2024-08-01T00:00:00Z","equity":10450.0}
        ]}
        """;

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
    public async Task Pbo_flags_mirror_strategies_as_overfit()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        double J(int i) => i % 2 == 0 ? 0.002 : -0.002;
        var a = Enumerable.Range(0, 16).Select(i => (i < 8 ? 0.01 : -0.01) + J(i)).ToArray();
        var b = Enumerable.Range(0, 16).Select(i => (i < 8 ? -0.01 : 0.01) + J(i)).ToArray();

        var response = await client.PostAsJsonAsync("/api/quant/pbo", new { Trials = new[] { a, b } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("verdict").GetString().Should().Be("Overfit");
        body.GetProperty("probabilityOfBacktestOverfitting").GetDouble().Should().BeGreaterThan(0.4);
    }

    [Fact]
    public async Task Pbo_rejects_a_single_trial()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var response = await client.PostAsJsonAsync("/api/quant/pbo",
            new { Trials = new[] { Enumerable.Range(0, 16).Select(i => 0.01).ToArray() } });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Integrity_of_a_completed_backtest_scores_from_its_stored_equity_curve()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        Guid completedId, runId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var uid = (await db.Users.FirstAsync(u => u.NormalizedEmail == Owner.ToUpperInvariant())).Id;
            var cbot = CBot.Create(uid, $"bot-{Guid.NewGuid():N}", [1, 2, 3]);
            var node = LocalNode.Create($"ln-{Guid.NewGuid():N}", "/var/app/data", 10, enabled: true);
            db.CBots.Add(cbot);
            db.Nodes.Add(node);
            var completed = ((StartingBacktestInstance)BacktestInstance.CreateStarting(uid, cbot.Id, node.Id,
                    new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), "{}"))
                .ToRunning("c1", Now).ToCompleted(Now.AddMinutes(1), EquityReportJson);
            var run = ((StartingRunInstance)RunInstance.CreateStarting(uid, cbot.Id, node.Id,
                new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"))).ToRunning("c2", Now);
            db.Instances.Add(completed);
            db.Instances.Add(run);
            await db.SaveChangesAsync();
            completedId = completed.Id.Value;
            runId = run.Id.Value;
        }

        var ok = await client.PostAsJsonAsync($"/api/quant/integrity/backtest/{completedId}", new { });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ok.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("verdict").GetString().Should().BeOneOf("Robust", "Fragile", "Overfit");
        body.GetProperty("observations").GetInt32().Should().BeGreaterThan(1);

        // A run instance has no backtest report → 404, so the UI keeps the integrity button disabled.
        (await client.PostAsJsonAsync($"/api/quant/integrity/backtest/{runId}", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
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
