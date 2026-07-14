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

    private const string SampleReportJson =
        """{"netProfit":1234.5,"maxDrawdown":210.0,"totalTrades":87,"winRate":0.56,"profitFactor":1.4,"sharpe":1.1}""";

    public static IEndpointRouteBuilder MapTestSeedEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/testseed").RequireAuthorization(AuthPolicies.UserOrAbove);

        // Seeds a minimal portfolio for the current user: a cBot + param set, one completed backtest
        // carrying a report, and one running instance. Enough to drive digest / exposure / tune / optimize
        // / analyze-backtest / post-mortem end to end against a configured AI provider.
        g.MapPost("/ai-portfolio", async (DataContext db, ICurrentUser user, TimeProvider clock, CancellationToken ct) =>
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
            await db.SaveChangesAsync(ct);

            var tag = new DockerImageTag("latest");
            var symbol = new Symbol("EURUSD");
            var timeframe = new Timeframe("h1");

            var completed = ((StartingBacktestInstance)BacktestInstance.CreateStarting(
                    uid, cbot.Id, node.Id, tag, symbol, timeframe, null))
                .ToRunning("seed-bt", now.AddMinutes(-30))
                .ToCompleted(now.AddMinutes(-5), SampleReportJson);
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
                completedInstanceId = completed.Id.Value,
                runningInstanceId = running.Id.Value
            });
        });

        return g;
    }
}
