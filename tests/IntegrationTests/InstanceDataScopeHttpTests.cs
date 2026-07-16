using System.Net.Http.Json;
using System.Text.Json;
using Core;
using Core.Accounts;
using Core.Constants;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nodes;
using Xunit;

namespace IntegrationTests;

// Two guarantees for the instance detail page + backtest data cache:
//  1. The shared market-data cache dir is keyed on the TRADING ACCOUNT (its number), so it is STABLE across
//     every backtest of that account (instance ids change every run) — that is what stops cTrader from
//     re-downloading the data each backtest.
//  2. The detail payload carries the cBot name so the browser tab title can show it.
public class InstanceDataScopeHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@datascope.local";
    private const string Password = "Owner_Pass_123!";
    private const long AccountNumber = 7_654_321L;
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

    [Fact]
    public async Task Data_scope_is_the_account_number_and_stable_across_backtests_of_that_account()
    {
        await using var app = CreateApp();
        _ = await LoginAsync(app);

        Guid firstId, secondId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
            var uid = (await db.Users.FirstAsync(u => u.NormalizedEmail == Owner.ToUpperInvariant())).Id;

            var cbot = CBot.Create(uid, $"bot-{Guid.NewGuid():N}", protector.Protect([1, 2, 3], EncryptionPurposes.CbotAlgo));
            db.CBots.Add(cbot);
            var cid = CTraderIdAccount.Create(uid, $"cid-{Guid.NewGuid():N}",
                protector.Protect("pw"u8, EncryptionPurposes.CtidPassword));
            var account = cid.AddTradingAccount(AccountNumber, "Pepperstone", false, "demo", BrokerAllowlist.FromNames([]));
            db.CTids.Add(cid);
            var node = LocalNode.Create($"ln-{Guid.NewGuid():N}", "/var/app/data", 10, enabled: true);
            db.Nodes.Add(node);

            // Two SEPARATE backtests of the same account — different instance ids.
            var first = ((StartingBacktestInstance)BacktestInstance.CreateStarting(uid, cbot.Id, node.Id,
                new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), "{}", account.Id)).ToRunning("c1", Now);
            var second = ((StartingBacktestInstance)BacktestInstance.CreateStarting(uid, cbot.Id, node.Id,
                new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), "{}", account.Id)).ToRunning("c2", Now);
            db.Instances.Add(first);
            db.Instances.Add(second);
            await db.SaveChangesAsync();
            firstId = first.Id.Value;
            secondId = second.Id.Value;
        }

        firstId.Should().NotBe(secondId);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var first = await db.Instances.Include(x => x.TradingAccount)
                .FirstAsync(x => x.Id == InstanceId.From(firstId));
            var second = await db.Instances.Include(x => x.TradingAccount)
                .FirstAsync(x => x.Id == InstanceId.From(secondId));

            var scope1 = ContainerCommandHelpers.DataScopeFor(first);
            var scope2 = ContainerCommandHelpers.DataScopeFor(second);

            scope1.Should().Be(AccountNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "the market-data cache is keyed on the trading account number");
            scope2.Should().Be(scope1,
                "two different backtests of the same account share ONE data dir, so the data is not re-downloaded");
        }
    }

    [Fact]
    public async Task Instance_detail_payload_includes_the_cbot_name_for_the_page_title()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        Guid runningId;
        string cbotName;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var uid = (await db.Users.FirstAsync(u => u.NormalizedEmail == Owner.ToUpperInvariant())).Id;
            var cbot = CBot.Create(uid, $"TitleBot-{Guid.NewGuid():N}", [1, 2, 3]);
            cbotName = cbot.Name;
            var node = LocalNode.Create($"ln-{Guid.NewGuid():N}", "/var/app/data", 10, enabled: true);
            db.CBots.Add(cbot);
            db.Nodes.Add(node);
            var running = ((StartingRunInstance)RunInstance.CreateStarting(uid, cbot.Id, node.Id,
                new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"))).ToRunning("c1", Now);
            db.Instances.Add(running);
            await db.SaveChangesAsync();
            runningId = running.Id.Value;
        }

        var detail = await (await client.GetAsync($"/api/instances/{runningId}")).Content.ReadFromJsonAsync<JsonElement>();

        detail.GetProperty("cbotName").GetString().Should().Be(cbotName,
            "the detail payload carries the cBot name so the browser title can show it");
    }
}
