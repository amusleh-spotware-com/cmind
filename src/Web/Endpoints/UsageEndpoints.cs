using Core;
using Core.Constants;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

/// <summary>
/// Owner/admin usage summary a cloud/VPS provider can meter and bill on (see the "For cloud & VPS
/// providers" docs). A read-only projection over existing state — users, the compute fleet, and the
/// backtest/run workload — so no new domain or persistence is introduced.
/// </summary>
public static class UsageEndpoints
{
    public static IEndpointRouteBuilder MapUsageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/usage", async (DataContext db, CancellationToken ct) =>
        {
            var usersTotal = await db.Users.CountAsync(ct);
            var nodesTotal = await db.Nodes.CountAsync(ct);
            var nodesOnline = await db.Nodes.CountAsync(
                n => n is ActiveRunNode || n is ActiveBacktestNode || n is ActiveMixedNode, ct);
            var cbotsTotal = await db.CBots.CountAsync(ct);
            var accountsTotal = await db.TradingAccounts.CountAsync(ct);
            var instancesTotal = await db.Instances.CountAsync(ct);
            var backtestsRunning = await db.Instances.OfType<RunningBacktestInstance>().CountAsync(ct);
            var runsRunning = await db.Instances.OfType<RunningRunInstance>().CountAsync(ct);

            return Results.Ok(new
            {
                users = new { total = usersTotal },
                nodes = new { total = nodesTotal, online = nodesOnline },
                instances = new { total = instancesTotal, backtestsRunning, runsRunning },
                cbots = new { total = cbotsTotal },
                tradingAccounts = new { total = accountsTotal }
            });
        }).RequireAuthorization(AuthPolicies.AdminOrAbove);

        return app;
    }
}
