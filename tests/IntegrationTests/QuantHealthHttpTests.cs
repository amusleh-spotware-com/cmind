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

public class QuantHealthHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@health.local";
    private const string Password = "Owner_Pass_123!";
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    // A long, rising equity curve (parsed by ContainerCommandHelpers.ParseEquityCurve) so a completed
    // backtest can be scored end to end from its stored report.
    private const string EquityReportJson =
        """
        {"equityHistory":[
          {"time":"2024-01-01T00:00:00Z","equity":10000.0},
          {"time":"2024-02-01T00:00:00Z","equity":10120.0},
          {"time":"2024-03-01T00:00:00Z","equity":10250.0},
          {"time":"2024-04-01T00:00:00Z","equity":10360.0},
          {"time":"2024-05-01T00:00:00Z","equity":10500.0},
          {"time":"2024-06-01T00:00:00Z","equity":10640.0},
          {"time":"2024-07-01T00:00:00Z","equity":10770.0},
          {"time":"2024-08-01T00:00:00Z","equity":10900.0},
          {"time":"2024-09-01T00:00:00Z","equity":11020.0},
          {"time":"2024-10-01T00:00:00Z","equity":11150.0},
          {"time":"2024-11-01T00:00:00Z","equity":11280.0},
          {"time":"2024-12-01T00:00:00Z","equity":11410.0}
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

    [Fact]
    public async Task Too_short_series_is_rejected()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/quant/health", new { Returns = new[] { 0.01 } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Health_of_a_completed_backtest_scores_from_its_stored_equity_curve()
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

        var ok = await client.PostAsJsonAsync($"/api/quant/health/backtest/{completedId}", new { });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ok.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("health").GetString().Should().BeOneOf("Healthy", "Degrading", "Decayed", "Unknown");
        body.GetProperty("observations").GetInt32().Should().BeGreaterThan(1);

        // A run instance has no backtest report → 404, so the UI keeps the health button disabled.
        (await client.PostAsJsonAsync($"/api/quant/health/backtest/{runId}", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
