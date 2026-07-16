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

// GET /api/instances/current resolves the CURRENT state of an instance lineage by (cBot, createdAt). A TPH
// transition replaces the entity with a new id while preserving CreatedAt, so this lets the detail page
// follow the instance from Running to its terminal state without going stale.
public class InstanceCurrentHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@current.local";
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

    private static string CurrentUrl(Guid cbotId, DateTimeOffset createdAt) =>
        $"/api/instances/current?cbotId={cbotId}&createdAt={Uri.EscapeDataString(createdAt.ToString("O"))}";

    [Fact]
    public async Task Current_follows_the_instance_across_a_transition_to_a_new_id()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        Guid runningId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var uid = (await db.Users.FirstAsync(u => u.NormalizedEmail == Owner.ToUpperInvariant())).Id;
            var cbot = CBot.Create(uid, $"bot-{Guid.NewGuid():N}", [1, 2, 3]);
            var node = LocalNode.Create($"ln-{Guid.NewGuid():N}", "/var/app/data", 10, enabled: true);
            db.CBots.Add(cbot);
            db.Nodes.Add(node);
            var running = ((StartingRunInstance)RunInstance.CreateStarting(uid, cbot.Id, node.Id,
                new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"))).ToRunning("c1", Now);
            db.Instances.Add(running);
            await db.SaveChangesAsync();
            runningId = running.Id.Value;
        }

        // The lineage key (cBot + createdAt) is read from the detail exactly as the browser would — matching
        // the persisted (microsecond-precision) CreatedAt value.
        var detail = await (await client.GetAsync($"/api/instances/{runningId}")).Content.ReadFromJsonAsync<JsonElement>();
        var cbotId = detail.GetProperty("cBotId").GetGuid();
        var createdAt = detail.GetProperty("createdAt").GetDateTimeOffset();

        var cur1 = await client.GetAsync(CurrentUrl(cbotId, createdAt));
        cur1.StatusCode.Should().Be(HttpStatusCode.OK);
        var body1 = await cur1.Content.ReadFromJsonAsync<JsonElement>();
        body1.GetProperty("status").GetString().Should().Be("Running");
        body1.GetProperty("id").GetGuid().Should().Be(runningId);

        // Transition Running -> Stopped (the id changes, CreatedAt is preserved).
        Guid stoppedId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var running = await db.Instances.OfType<RunningRunInstance>().FirstAsync(x => x.Id == InstanceId.From(runningId));
            var stopped = running.ToStopped(Now.AddMinutes(1));
            db.Instances.Remove(running);
            db.Instances.Add(stopped);
            await db.SaveChangesAsync();
            stoppedId = stopped.Id.Value;
        }
        stoppedId.Should().NotBe(runningId);

        var cur2 = await client.GetAsync(CurrentUrl(cbotId, createdAt));
        cur2.StatusCode.Should().Be(HttpStatusCode.OK);
        var body2 = await cur2.Content.ReadFromJsonAsync<JsonElement>();
        body2.GetProperty("status").GetString().Should().Be("Stopped",
            "the lineage now resolves to the terminal instance, so the detail page can follow it");
        body2.GetProperty("id").GetGuid().Should().Be(stoppedId);
    }

    [Fact]
    public async Task Current_for_an_unknown_lineage_is_not_found()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.GetAsync(CurrentUrl(Guid.NewGuid(), Now))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
