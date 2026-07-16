using Core;
using Core.Constants;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

// TEST-ONLY seeding endpoints. Mapped ONLY when the app runs in the Development environment AND
// App:TestSeed:Enabled is true (guarded twice in Program.cs) — never in Production. They exist so the
// E2E suite can create the in-DB domain state that the data-dependent AI features read (portfolio digest,
// live exposure, decay/tune, optimize, backtest analysis, post-mortem) WITHOUT Docker, nodes, or a broker:
// terminal Instances carry no container, so the same domain factories + state transitions the integration
// tests use produce a completed backtest (with a report) and a running instance directly.
public static class TestSeedEndpoints
{
    // Config gate — dev-only, never a production route. Read in Program.cs alongside IsDevelopment().
    public const string EnabledKey = "App:TestSeed:Enabled";

    // Carries an equityHistory curve (parsed by ContainerCommandHelpers.ParseEquityCurve) so the seeded
    // backtest drives the detail equity chart AND the Backtest Integrity Lab (which derives a return series
    // from the curve) end to end without Docker.
    private const string SampleReportJson =
        """
        {"netProfit":1234.5,"maxDrawdown":210.0,"totalTrades":87,"winRate":0.56,"profitFactor":1.4,"sharpe":1.1,
         "equityHistory":[
           {"time":"2024-01-01T00:00:00Z","equity":10000.0},
           {"time":"2024-02-01T00:00:00Z","equity":10120.0},
           {"time":"2024-03-01T00:00:00Z","equity":10080.0},
           {"time":"2024-04-01T00:00:00Z","equity":10210.0},
           {"time":"2024-05-01T00:00:00Z","equity":10190.0},
           {"time":"2024-06-01T00:00:00Z","equity":10320.0},
           {"time":"2024-07-01T00:00:00Z","equity":10280.0},
           {"time":"2024-08-01T00:00:00Z","equity":10450.0},
           {"time":"2024-09-01T00:00:00Z","equity":10410.0},
           {"time":"2024-10-01T00:00:00Z","equity":10560.0},
           {"time":"2024-11-01T00:00:00Z","equity":10520.0},
           {"time":"2024-12-01T00:00:00Z","equity":10700.0}
         ]}
        """;

    public static IEndpointRouteBuilder MapTestSeedEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/testseed").RequireAuthorization(AuthPolicies.UserOrAbove);

        // Seeds a minimal portfolio for the current user: a cBot + param set, one completed backtest
        // carrying a report, and one running instance. Enough to drive digest / exposure / tune / optimize
        // / analyze-backtest / post-mortem end to end against a configured AI provider.
        g.MapPost("/ai-portfolio", async (DataContext db, ICurrentUser user, TimeProvider clock,
            ISecretProtector protector, CancellationToken ct) =>
        {
            if (user.UserId is not { } uid) return Results.Unauthorized();
            var now = clock.GetUtcNow();

            var node = await db.Nodes.OfType<LocalNode>().FirstOrDefaultAsync(ct);
            if (node is null)
            {
                node = LocalNode.Create($"seed-{Guid.NewGuid():N}", FilePaths.NodeDataDirDefault,
                    LocalNodeDefaults.MaxInstances, enabled: true);
                db.Nodes.Add(node);
                await db.SaveChangesAsync(ct);
            }

            var cbot = CBot.Create(uid, $"seed-bot-{Guid.NewGuid():N}", []);
            db.CBots.Add(cbot);
            await db.SaveChangesAsync(ct);

            var paramSet = ParamSet.Create(uid, cbot.Id, "seed-params", """{"period":14}""");
            db.ParamSets.Add(paramSet);

            // A trading account for the completed backtest, so the Edit dialog can render the account number
            // and parameter-set name (never the raw Guid) when it prefills.
            var cid = CTraderIdAccount.Create(uid, $"seed-cid-{Guid.NewGuid():N}",
                protector.Protect("pw"u8, EncryptionPurposes.CtidPassword));
            var account = cid.AddTradingAccount(5_800_000L + Random.Shared.Next(190_000), "SeedBroker",
                isLive: false, "seed", Core.Accounts.BrokerAllowlist.Unrestricted);
            db.CTids.Add(cid);
            await db.SaveChangesAsync(ct);

            var tag = new DockerImageTag("latest");
            var symbol = new Symbol("EURUSD");
            var timeframe = new Timeframe("h1");

            var completed = ((StartingBacktestInstance)BacktestInstance.CreateStarting(
                    uid, cbot.Id, node.Id, tag, symbol, timeframe,
                    """{"from":"2024-01-01","to":"2024-02-01","balance":"10000"}""", account.Id, paramSet.Id))
                .ToRunning("seed-bt", now.AddMinutes(-30))
                .ToCompleted(now.AddMinutes(-5), SampleReportJson,
                    reportHtml: "<html><body><h1>Seed backtest report</h1></body></html>");
            completed.CaptureConsoleLog("seed backtest console line 1\nseed backtest console line 2");
            completed.ClearDomainEvents(); // seeding: no node/side-effect dispatch on the transitions
            db.Instances.Add(completed);
            await db.SaveChangesAsync(ct);

            var running = ((StartingRunInstance)RunInstance.CreateStarting(
                    uid, cbot.Id, node.Id, tag, symbol, timeframe, paramSetId: paramSet.Id))
                .ToRunning("seed-run", now.AddMinutes(-10));
            running.ClearDomainEvents();
            db.Instances.Add(running);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                cbotId = cbot.Id.Value,
                paramSetId = paramSet.Id.Value,
                accountNumber = account.AccountNumber,
                completedInstanceId = completed.Id.Value,
                runningInstanceId = running.Id.Value
            });
        });

        return g;
    }
}
