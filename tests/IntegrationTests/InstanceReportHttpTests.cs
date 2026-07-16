using System.Net;
using System.Net.Http.Json;
using Core;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

// Downloading the persisted JSON/HTML backtest report: available only for a COMPLETED backtest that
// produced them; every other state (running, run instance) is a 404 so the UI keeps the buttons disabled.
public class InstanceReportHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@report.local";
    private const string Password = "Owner_Pass_123!";
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private const string ReportJson = """{"netProfit":100.0,"totalTrades":3}""";
    private const string ReportHtml = "<html><body><h1>report</h1></body></html>";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:Execution", "true");
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    private static (CBot cbot, LocalNode node) Seed(DataContext db, UserId uid)
    {
        var cbot = CBot.Create(uid, $"bot-{Guid.NewGuid():N}", [1, 2, 3]);
        var node = LocalNode.Create($"ln-{Guid.NewGuid():N}", "/var/app/data", 10, enabled: true);
        db.CBots.Add(cbot);
        db.Nodes.Add(node);
        return (cbot, node);
    }

    [Fact]
    public async Task Completed_backtest_reports_are_downloadable_with_the_right_content()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        Guid completedId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var uid = (await db.Users.FirstAsync(u => u.NormalizedEmail == Owner.ToUpperInvariant())).Id;
            var (cbot, node) = Seed(db, uid);
            var completed = ((StartingBacktestInstance)BacktestInstance.CreateStarting(uid, cbot.Id, node.Id,
                    new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), "{}"))
                .ToRunning("c1", Now).ToCompleted(Now.AddMinutes(1), ReportJson, reportHtml: ReportHtml);
            db.Instances.Add(completed);
            await db.SaveChangesAsync();
            completedId = completed.Id.Value;
        }

        // The list flags both reports as available.
        var list = await (await client.GetAsync("/api/instances/")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var row = list.EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == completedId);
        row.GetProperty("hasReportJson").GetBoolean().Should().BeTrue();
        row.GetProperty("hasReportHtml").GetBoolean().Should().BeTrue();

        var json = await client.GetAsync($"/api/instances/{completedId}/report.json");
        json.StatusCode.Should().Be(HttpStatusCode.OK);
        (await json.Content.ReadAsStringAsync()).Should().Contain("netProfit");

        var html = await client.GetAsync($"/api/instances/{completedId}/report.html");
        html.StatusCode.Should().Be(HttpStatusCode.OK);
        (await html.Content.ReadAsStringAsync()).Should().Contain("<h1>report</h1>");
    }

    [Fact]
    public async Task Reports_are_not_found_for_a_running_backtest_or_a_run_instance()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        Guid runningBacktestId, runId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var uid = (await db.Users.FirstAsync(u => u.NormalizedEmail == Owner.ToUpperInvariant())).Id;
            var (cbot, node) = Seed(db, uid);
            var runningBacktest = ((StartingBacktestInstance)BacktestInstance.CreateStarting(uid, cbot.Id, node.Id,
                new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), "{}")).ToRunning("c1", Now);
            var run = ((StartingRunInstance)RunInstance.CreateStarting(uid, cbot.Id, node.Id,
                new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"))).ToRunning("c2", Now);
            db.Instances.Add(runningBacktest);
            db.Instances.Add(run);
            await db.SaveChangesAsync();
            runningBacktestId = runningBacktest.Id.Value;
            runId = run.Id.Value;
        }

        (await client.GetAsync($"/api/instances/{runningBacktestId}/report.json")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/instances/{runningBacktestId}/report.html")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/instances/{runId}/report.json")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
