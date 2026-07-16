using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Core;
using Core.Accounts;
using Core.Constants;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace IntegrationTests;

// POST /api/instances/{id}/start re-launches a terminal instance (run or backtest) with the same config, and
// a completed backtest's persisted console log is downloadable. A fake dispatcher makes the launch
// deterministic without Docker.
public class InstanceRestartHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@restart.local";
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
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IContainerDispatcherFactory>();
                s.AddScoped<IContainerDispatcherFactory, FakeDispatcherFactory>();
            });
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<(DataContext db, UserId uid, ISecretProtector protector)> ScopeAsync(WebApplicationFactory<Program> app)
    {
        var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var uid = (await db.Users.FirstAsync(u => u.NormalizedEmail == Owner.ToUpperInvariant())).Id;
        return (db, uid, protector);
    }

    private static (CBot cbot, LocalNode node, TradingAccountId accountId) SeedCommon(
        DataContext db, UserId uid, ISecretProtector protector)
    {
        var cbot = CBot.Create(uid, $"bot-{Guid.NewGuid():N}", protector.Protect([1, 2, 3], EncryptionPurposes.CbotAlgo));
        db.CBots.Add(cbot);
        var cid = CTraderIdAccount.Create(uid, $"cid-{Guid.NewGuid():N}",
            protector.Protect("pw"u8, EncryptionPurposes.CtidPassword));
        var account = cid.AddTradingAccount(6_000_000L + Random.Shared.Next(900_000), "Pepperstone", false, "demo",
            BrokerAllowlist.FromNames([]));
        db.CTids.Add(cid);
        var node = LocalNode.Create($"ln-{Guid.NewGuid():N}", "/var/app/data", 10, enabled: true);
        db.Nodes.Add(node);
        return (cbot, node, account.Id);
    }

    [Fact]
    public async Task Restart_a_stopped_run_replaces_it_with_a_new_running_instance()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        Guid stoppedId;
        string cbotName;
        var (db, uid, protector) = await ScopeAsync(app);
        await using (db)
        {
            var (cbot, node, accountId) = SeedCommon(db, uid, protector);
            cbotName = cbot.Name;
            var stopped = ((StartingRunInstance)RunInstance.CreateStarting(uid, cbot.Id, node.Id,
                    new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), accountId))
                .ToRunning("c1", Now).ToStopped(Now.AddMinutes(1));
            db.Instances.Add(stopped);
            await db.SaveChangesAsync();
            stoppedId = stopped.Id.Value;
        }

        var res = await client.PostAsync($"/api/instances/{stoppedId}/start", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var newId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        newId.Should().NotBe(stoppedId);

        var list = await (await client.GetAsync("/api/instances/")).Content.ReadFromJsonAsync<JsonElement>();
        var rows = list.EnumerateArray().Where(r => r.GetProperty("cBot").GetString() == cbotName).ToList();
        rows.Should().ContainSingle("the restart replaces the old instance rather than duplicating it");
        rows[0].GetProperty("id").GetGuid().Should().Be(newId);
        rows[0].GetProperty("status").GetString().Should().Be("Running",
            "the launch succeeded — proving the trading account (and thus --pwd-file) was loaded");
    }

    [Fact]
    public async Task Restart_a_completed_backtest_replaces_it_with_a_new_running_backtest()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        Guid completedId;
        string cbotName;
        var (db, uid, protector) = await ScopeAsync(app);
        await using (db)
        {
            var (cbot, node, accountId) = SeedCommon(db, uid, protector);
            cbotName = cbot.Name;
            var completed = ((StartingBacktestInstance)BacktestInstance.CreateStarting(uid, cbot.Id, node.Id,
                    new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), "{}", accountId))
                .ToRunning("c1", Now).ToCompleted(Now.AddMinutes(1));
            db.Instances.Add(completed);
            await db.SaveChangesAsync();
            completedId = completed.Id.Value;
        }

        var res = await client.PostAsync($"/api/instances/{completedId}/start", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var newId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        newId.Should().NotBe(completedId);

        var list = await (await client.GetAsync("/api/instances/")).Content.ReadFromJsonAsync<JsonElement>();
        var rows = list.EnumerateArray().Where(r => r.GetProperty("cBot").GetString() == cbotName).ToList();
        rows.Should().ContainSingle("the restart replaces the old backtest rather than duplicating it");
        rows[0].GetProperty("kind").GetString().Should().Be("Backtest");
        rows[0].GetProperty("status").GetString().Should().Be("Running",
            "the backtest launched successfully — proving the trading account (and thus --pwd-file) was loaded");
    }

    [Fact]
    public async Task Edit_a_stopped_backtest_relaunches_it_with_the_changed_symbol_and_settings()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        Guid completedId;
        string cbotName;
        Guid accountId;
        var (db, uid, protector) = await ScopeAsync(app);
        await using (db)
        {
            var (cbot, node, acctId) = SeedCommon(db, uid, protector);
            cbotName = cbot.Name;
            accountId = acctId.Value;
            var completed = ((StartingBacktestInstance)BacktestInstance.CreateStarting(uid, cbot.Id, node.Id,
                    new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"),
                    """{"from":"2024-01-01","to":"2024-02-01","balance":"10000"}""", acctId))
                .ToRunning("c1", Now).ToCompleted(Now.AddMinutes(1));
            db.Instances.Add(completed);
            await db.SaveChangesAsync();
            completedId = completed.Id.Value;
        }

        var res = await client.PostAsJsonAsync($"/api/instances/{completedId}/edit", new
        {
            TradingAccountId = accountId,
            Symbol = "GBPUSD",
            Timeframe = "m5",
            ParamSetId = (Guid?)null,
            DockerImageTag = "latest",
            BacktestSettingsJson = """{"from":"2024-03-01","to":"2024-04-01","balance":"25000"}"""
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var newId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        newId.Should().NotBe(completedId);

        var list = await (await client.GetAsync("/api/instances/")).Content.ReadFromJsonAsync<JsonElement>();
        var rows = list.EnumerateArray().Where(r => r.GetProperty("cBot").GetString() == cbotName).ToList();
        rows.Should().ContainSingle("editing replaces the old instance, not duplicates it");
        rows[0].GetProperty("symbol").GetString().Should().Be("GBPUSD", "the edited symbol took effect");
        rows[0].GetProperty("timeframe").GetString().Should().Be("m5");
        rows[0].GetProperty("kind").GetString().Should().Be("Backtest");
        rows[0].GetProperty("status").GetString().Should().Be("Running");
    }

    [Fact]
    public async Task Edit_an_active_instance_is_a_conflict()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        Guid runningId;
        Guid accountId;
        var (db, uid, protector) = await ScopeAsync(app);
        await using (db)
        {
            var (cbot, node, acctId) = SeedCommon(db, uid, protector);
            accountId = acctId.Value;
            var running = ((StartingRunInstance)RunInstance.CreateStarting(uid, cbot.Id, node.Id,
                    new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), acctId))
                .ToRunning("c1", Now);
            db.Instances.Add(running);
            await db.SaveChangesAsync();
            runningId = running.Id.Value;
        }

        (await client.PostAsJsonAsync($"/api/instances/{runningId}/edit", new
        {
            TradingAccountId = accountId, Symbol = "GBPUSD", Timeframe = "m5",
            ParamSetId = (Guid?)null, DockerImageTag = "latest", BacktestSettingsJson = (string?)null
        })).StatusCode.Should().Be(HttpStatusCode.Conflict, "a running instance cannot be edited");
    }

    [Fact]
    public async Task Restart_an_active_instance_is_a_conflict()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        Guid runningId;
        var (db, uid, protector) = await ScopeAsync(app);
        await using (db)
        {
            var (cbot, node, accountId) = SeedCommon(db, uid, protector);
            var running = ((StartingRunInstance)RunInstance.CreateStarting(uid, cbot.Id, node.Id,
                    new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), accountId))
                .ToRunning("c1", Now);
            db.Instances.Add(running);
            await db.SaveChangesAsync();
            runningId = running.Id.Value;
        }

        (await client.PostAsync($"/api/instances/{runningId}/start", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "a running instance cannot be restarted");
    }

    [Fact]
    public async Task Completed_backtest_console_log_is_downloadable()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        Guid completedId;
        var (db, uid, protector) = await ScopeAsync(app);
        await using (db)
        {
            var (cbot, node, accountId) = SeedCommon(db, uid, protector);
            var running = ((StartingBacktestInstance)BacktestInstance.CreateStarting(uid, cbot.Id, node.Id,
                    new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), "{}", accountId))
                .ToRunning("c1", Now);
            running.CaptureConsoleLog("backtest output line");
            var completed = running.ToCompleted(Now.AddMinutes(1));
            db.Instances.Add(completed);
            await db.SaveChangesAsync();
            completedId = completed.Id.Value;
        }

        var res = await client.GetAsync($"/api/instances/{completedId}/logs");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain("backtest output line");
    }

    private sealed class FakeDispatcherFactory : IContainerDispatcherFactory
    {
        public IContainerDispatcher For(Node node) => new FakeDispatcher();
        public IContainerDispatcher For(Instance instance) => new FakeDispatcher();
    }

    private sealed class FakeDispatcher : IContainerDispatcher
    {
        public Task<string> StartAsync(Instance instance, byte[] algoBytes, string paramJson, CancellationToken ct)
        {
            // Mirror the real dispatcher's requirement: to pass --ctid/--pwd-file/--account to the CLI the
            // TradingAccount navigation (with its cID) must be populated. If the restart forgot to load it,
            // fail — this is the regression guard for the "Should be specified parameter: --pwd-file" bug.
            if (instance.TradingAccountId is not null && (instance.TradingAccount is null || instance.TradingAccount.CTid is null))
                throw new InvalidOperationException("TradingAccount navigation was not loaded");
            return Task.FromResult("container-restarted");
        }

        public Task StopAsync(Instance instance, CancellationToken ct) => Task.CompletedTask;

#pragma warning disable CS1998
        public async IAsyncEnumerable<string> TailLogsAsync(Instance instance, [EnumeratorCancellation] CancellationToken ct)
        {
            yield break;
        }
#pragma warning restore CS1998

        public Task<NodeStats> CollectStatsAsync(Node node, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> GetBacktestDataSizeAsync(Node node, CancellationToken ct) => Task.FromResult(0L);
        public Task CleanBacktestDataAsync(Node node, UserId? userId, CancellationToken ct) => Task.CompletedTask;
        public Task<bool?> IsRunningAsync(Instance instance, CancellationToken ct) => Task.FromResult<bool?>(null);
        public Task<int?> GetExitCodeAsync(Instance instance, CancellationToken ct) => Task.FromResult<int?>(null);
        public Task<string?> ReadReportAsync(Instance instance, CancellationToken ct) => Task.FromResult<string?>(null);
    }
}
