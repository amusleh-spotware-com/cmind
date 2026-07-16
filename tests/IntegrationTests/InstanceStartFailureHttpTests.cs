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

// When the container fails to launch, POST /api/instances/ must record a Failed instance and return OK (so
// the caller shows it in the list) instead of a 500 with an orphaned Starting row. Uses a fake dispatcher
// whose StartAsync throws, so the failure path is deterministic without Docker.
public class InstanceStartFailureHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@startfail.local";
    private const string Password = "Owner_Pass_123!";

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
                s.AddScoped<IContainerDispatcherFactory, ThrowingDispatcherFactory>();
            });
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<(Guid cbotId, Guid accountId, string cbotName)> SeedRunnableAsync(WebApplicationFactory<Program> app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var uid = (await db.Users.FirstAsync(u => u.NormalizedEmail == Owner.ToUpperInvariant())).Id;

        var cbotName = $"runbot-{Guid.NewGuid():N}";
        var cbot = CBot.Create(uid, cbotName, protector.Protect([1, 2, 3], EncryptionPurposes.CbotAlgo));
        db.CBots.Add(cbot);

        var cid = CTraderIdAccount.Create(uid, $"cid-{Guid.NewGuid():N}",
            protector.Protect("pw"u8, EncryptionPurposes.CtidPassword));
        var account = cid.AddTradingAccount(5_000_000L + Random.Shared.Next(900_000), "Pepperstone", false, "demo",
            BrokerAllowlist.FromNames([]));
        db.CTids.Add(cid);

        db.Nodes.Add(LocalNode.Create($"ln-{Guid.NewGuid():N}", "/var/app/data", 10, enabled: true));
        await db.SaveChangesAsync();
        return (cbot.Id.Value, account.Id.Value, cbotName);
    }

    [Fact]
    public async Task Failed_launch_is_recorded_and_appears_in_the_list_not_a_500()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var (cbotId, accountId, cbotName) = await SeedRunnableAsync(app);

        var res = await client.PostAsJsonAsync("/api/instances/", new
        {
            CBotId = cbotId,
            TradingAccountId = accountId,
            Symbol = "EURUSD",
            Timeframe = "h1",
            ParamSetId = (Guid?)null,
            DockerImageTag = "latest",
            Type = "Run",
            BacktestSettingsJson = (string?)null
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK, "a container-start failure is a recorded Failed instance, not a 500");

        var list = await (await client.GetAsync("/api/instances/")).Content.ReadFromJsonAsync<JsonElement>();
        var row = list.EnumerateArray().Single(r => r.GetProperty("cBot").GetString() == cbotName);
        row.GetProperty("status").GetString().Should().Be("Failed",
            "the instance must appear immediately as Failed after the launch error");
    }

    private sealed class ThrowingDispatcherFactory : IContainerDispatcherFactory
    {
        public IContainerDispatcher For(Node node) => new ThrowingDispatcher();
        public IContainerDispatcher For(Instance instance) => new ThrowingDispatcher();
    }

    private sealed class ThrowingDispatcher : IContainerDispatcher
    {
        public Task<string> StartAsync(Instance instance, byte[] algoBytes, string paramJson, CancellationToken ct) =>
            throw new InvalidOperationException("simulated container start failure");

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
