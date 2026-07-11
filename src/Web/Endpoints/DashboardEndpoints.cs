using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/dashboard").RequireAuthorization();

        g.MapGet("/overview", async (string? period, DataContext db, ICurrentUser u, TimeProvider time) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var overview = await DashboardQuery.BuildAsync(
                db, uid, u.IsAtLeast("Admin"), DashboardPeriods.Parse(period), time.GetUtcNow());
            return Results.Ok(overview);
        });

        g.MapGet("/stats", async (DataContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var isAdmin = u.IsAtLeast("Admin");

            var cBotCount = await db.CBots.CountAsync(c => c.UserId == uid);
            var projectCount = await db.CBotSourceProjects.CountAsync(p => p.UserId == uid);
            var paramSetCount = await db.ParamSets.CountAsync(p => p.UserId == uid);
            var tradingAccountCount = await db.TradingAccounts.CountAsync(t => t.CTid.UserId == uid);
            var ctidCount = await db.CTids.CountAsync(c => c.UserId == uid);

            var counts = await db.Instances.Where(i => i.UserId == uid)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Running = g.Count(i => i is RunningRunInstance || i is StartingRunInstance),
                    BacktestRunning = g.Count(i => i is RunningBacktestInstance || i is StartingBacktestInstance),
                    Pending = g.Count(i => i is PendingRunInstance || i is PendingBacktestInstance),
                    Failed = g.Count(i => i is FailedRunInstance || i is FailedBacktestInstance),
                    Completed = g.Count(i => i is StoppedRunInstance || i is CompletedBacktestInstance),
                    Total = g.Count()
                })
                .FirstOrDefaultAsync();
            var runningCount = counts?.Running ?? 0;
            var backtestRunningCount = counts?.BacktestRunning ?? 0;
            var pendingCount = counts?.Pending ?? 0;
            var failedCount = counts?.Failed ?? 0;
            var completedCount = counts?.Completed ?? 0;
            var totalInstances = counts?.Total ?? 0;

            var mcpKeyCount = await db.McpApiKeys.CountAsync(k => k.UserId == uid && k.RevokedAt == null);

            int? userCount = null, nodeCount = null, activeNodes = null;
            if (isAdmin)
            {
                userCount = await db.Users.CountAsync();
                nodeCount = await db.Nodes.CountAsync();
                activeNodes = await db.Nodes
                    .CountAsync(n => n is ActiveRunNode || n is ActiveBacktestNode || n is ActiveMixedNode
                                     || (n is LocalNode && ((LocalNode)n).Enabled));
            }

            return Results.Ok(new
            {
                cBots = cBotCount,
                projects = projectCount,
                paramSets = paramSetCount,
                tradingAccounts = tradingAccountCount,
                ctids = ctidCount,
                runningInstances = runningCount,
                runningBacktests = backtestRunningCount,
                pendingInstances = pendingCount,
                failedInstances = failedCount,
                completedInstances = completedCount,
                totalInstances,
                mcpKeys = mcpKeyCount,
                users = userCount,
                nodes = nodeCount,
                activeNodes,
                isAdmin
            });
        });

        return app;
    }
}
