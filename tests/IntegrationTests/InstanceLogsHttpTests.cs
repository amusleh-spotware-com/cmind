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

// A terminal instance's captured console log stays downloadable: GET /api/instances/{id}/logs returns the
// persisted text, and the list feed marks the row as having logs. An instance with no logs 404s.
public class InstanceLogsHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@instlogs.local";
    private const string Password = "Owner_Pass_123!";
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

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

    private static async Task<Guid> SeedStoppedInstanceWithLogsAsync(WebApplicationFactory<Program> app, string log)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var uid = (await db.Users.FirstAsync(u => u.NormalizedEmail == Owner.ToUpperInvariant())).Id;

        var cbot = CBot.Create(uid, $"logbot-{Guid.NewGuid():N}", [1, 2, 3]);
        var node = LocalNode.Create($"node-{Guid.NewGuid():N}", "/var/app/data", 10, enabled: true);
        db.CBots.Add(cbot);
        db.Nodes.Add(node);

        var running = ((StartingRunInstance)RunInstance.CreateStarting(uid, cbot.Id, node.Id,
            new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"))).ToRunning("c1", Now);
        running.CaptureConsoleLog(log);
        var stopped = running.ToStopped(Now.AddMinutes(1));
        db.Instances.Add(stopped);

        await db.SaveChangesAsync();
        return stopped.Id.Value;
    }

    [Fact]
    public async Task Downloads_the_persisted_console_log_of_a_terminal_instance()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var id = await SeedStoppedInstanceWithLogsAsync(app, "hello logs\nsecond line");

        var res = await client.GetAsync($"/api/instances/{id}/logs");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain("hello logs").And.Contain("second line");
    }

    [Fact]
    public async Task List_marks_an_instance_with_logs_as_downloadable()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var id = await SeedStoppedInstanceWithLogsAsync(app, "some output");

        var list = await (await client.GetAsync("/api/instances/")).Content.ReadFromJsonAsync<JsonElement>();
        var row = list.EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == id);
        row.GetProperty("hasLogs").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Logs_for_a_missing_instance_is_not_found()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.GetAsync($"/api/instances/{Guid.NewGuid()}/logs")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
