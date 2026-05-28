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

        g.MapGet("/stats", async (CtwDbContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var isAdmin = u.IsAtLeast("Admin");

            var cBotCount = await db.CBots.CountAsync(c => c.UserId == uid);
            var projectCount = await db.CBotSourceProjects.CountAsync(p => p.UserId == uid);
            var paramSetCount = await db.ParamSets.CountAsync(p => p.UserId == uid);
            var tradingAccountCount = await db.TradingAccounts.CountAsync(t => t.CTid.UserId == uid);
            var ctidCount = await db.CTids.CountAsync(c => c.UserId == uid);

            var allInstances = db.Instances.Where(i => i.UserId == uid);
            var runningCount = await allInstances
                .CountAsync(i => i is RunningRunInstance || i is StartingRunInstance);
            var backtestRunningCount = await allInstances
                .CountAsync(i => i is RunningBacktestInstance || i is StartingBacktestInstance);
            var pendingCount = await allInstances
                .CountAsync(i => i is PendingRunInstance || i is PendingBacktestInstance);
            var failedCount = await allInstances
                .CountAsync(i => i is FailedRunInstance || i is FailedBacktestInstance);
            var completedCount = await allInstances
                .CountAsync(i => i is StoppedRunInstance || i is CompletedBacktestInstance);
            var totalInstances = await allInstances.CountAsync();

            var mcpKeyCount = await db.McpApiKeys.CountAsync(k => k.UserId == uid && k.RevokedAt == null);

            int? userCount = null, nodeCount = null, activeNodes = null;
            if (isAdmin)
            {
                userCount = await db.Users.CountAsync();
                nodeCount = await db.Nodes.CountAsync();
                activeNodes = await db.Nodes
                    .CountAsync(n => n is ActiveRunNode || n is ActiveBacktestNode || n is ActiveMixedNode);
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
