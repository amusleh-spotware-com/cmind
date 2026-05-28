using System.ComponentModel;
using System.Security.Claims;
using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Mcp.Tools;

[McpServerToolType]
public sealed class InstanceTools(CtwDbContext db, IHttpContextAccessor http)
{
    private const int MaxResults = 100;

    private UserId? CurrentUserId => Guid.TryParse(
        http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? UserId.From(g) : null;

    [McpServerTool, Description("List user's instances.")]
    public async Task<object> ListInstances()
    {
        if (CurrentUserId is not { } uid) return Array.Empty<object>();
        var rows = await db.Instances.Where(i => i.UserId == uid)
            .OrderByDescending(i => i.CreatedAt).Take(MaxResults)
            .ToListAsync();
        return rows.Select(i => new
        {
            i.Id,
            Kind = i.KindName,
            Status = i.StatusName,
            i.Symbol,
            i.Timeframe,
            StartedAt = (i as RunningRunInstance)?.StartedAt
                        ?? (i as RunningBacktestInstance)?.StartedAt
                        ?? (i as StoppingRunInstance)?.StartedAt
                        ?? (i as StoppingBacktestInstance)?.StartedAt
                        ?? (i as StoppedRunInstance)?.StartedAt
                        ?? (i as CompletedBacktestInstance)?.StartedAt
                        ?? (i as FailedRunInstance)?.StartedAt
                        ?? (i as FailedBacktestInstance)?.StartedAt,
            StoppedAt = (i as StoppedRunInstance)?.StoppedAt
                        ?? (i as CompletedBacktestInstance)?.StoppedAt
                        ?? (i as FailedRunInstance)?.StoppedAt
                        ?? (i as FailedBacktestInstance)?.StoppedAt
        }).ToList();
    }

    [McpServerTool, Description("Get backtest result JSON path for completed instance.")]
    public async Task<object?> GetBacktestResult([Description("Instance ID")] Guid instanceId)
    {
        if (CurrentUserId is not { } uid) return null;
        var iid = InstanceId.From(instanceId);
        var i = await db.Instances.OfType<CompletedBacktestInstance>()
            .FirstOrDefaultAsync(x => x.Id == iid && x.UserId == uid);
        return i is null ? null : new { i.Id, Status = i.StatusName, i.ResultJsonPath };
    }
}
