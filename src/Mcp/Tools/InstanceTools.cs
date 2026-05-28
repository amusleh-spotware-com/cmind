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

    private Guid? UserId => Guid.TryParse(
        http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : null;

    [McpServerTool, Description("List user's instances.")]
    public async Task<object> ListInstances()
    {
        if (UserId is not { } uid) return Array.Empty<object>();
        var rows = await db.Instances.Where(i => i.UserId == uid)
            .OrderByDescending(i => i.CreatedAt).Take(MaxResults)
            .Select(i => new { i.Id, i.Type, i.Status, i.Symbol, i.Timeframe, i.StartedAt, i.StoppedAt })
            .ToListAsync();
        return rows.Select(i => new { i.Id, Type = i.Type.Name, Status = i.Status.Name,
            i.Symbol, i.Timeframe, i.StartedAt, i.StoppedAt }).ToList();
    }

    [McpServerTool, Description("Get backtest result JSON path for completed instance.")]
    public async Task<object?> GetBacktestResult([Description("Instance ID")] Guid instanceId)
    {
        if (UserId is not { } uid) return null;
        var backtest = InstanceType.Backtest;
        var i = await db.Instances
            .FirstOrDefaultAsync(x => x.Id == instanceId && x.UserId == uid && x.Type == backtest);
        return i is null ? null : new { i.Id, Status = i.Status.Name, i.ResultJsonPath };
    }
}
