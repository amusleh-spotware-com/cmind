using System.ComponentModel;
using System.Security.Claims;
using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Mcp.Tools;

[McpServerToolType]
public sealed class InstanceTools
{
    private readonly CtwDbContext _db;
    private readonly IHttpContextAccessor _http;

    public InstanceTools(CtwDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    private Guid? UserId => Guid.TryParse(
        _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : null;

    [McpServerTool, Description("List user's instances.")]
    public async Task<object> ListInstances()
    {
        if (UserId is not { } uid) return Array.Empty<object>();
        return await _db.Instances.Where(i => i.UserId == uid)
            .OrderByDescending(i => i.CreatedAt).Take(100)
            .Select(i => new { i.Id, i.Type, i.Status, i.Symbol, i.Timeframe, i.StartedAt, i.StoppedAt })
            .ToListAsync();
    }

    [McpServerTool, Description("Get backtest result JSON path for completed instance.")]
    public async Task<object?> GetBacktestResult([Description("Instance ID")] Guid instanceId)
    {
        if (UserId is not { } uid) return null;
        var i = await _db.Instances
            .FirstOrDefaultAsync(x => x.Id == instanceId && x.UserId == uid && x.Type == InstanceType.Backtest);
        return i is null ? null : new { i.Id, i.Status, i.ResultJsonPath };
    }
}
