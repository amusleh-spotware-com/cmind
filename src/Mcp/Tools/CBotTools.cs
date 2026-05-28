using System.ComponentModel;
using System.Security.Claims;
using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Mcp.Tools;

[McpServerToolType]
public sealed class CBotTools(CtwDbContext db, IHttpContextAccessor http)
{
    private UserId? CurrentUserId => Guid.TryParse(
        http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? UserId.From(g) : null;

    [McpServerTool, Description("List user's cBots.")]
    public async Task<object> ListCBots()
    {
        if (CurrentUserId is not { } uid) return Array.Empty<object>();
        return await db.CBots.Where(c => c.UserId == uid)
            .Select(c => new { c.Id, c.Name, c.Version, c.CreatedAt })
            .ToListAsync();
    }

    [McpServerTool, Description("List user's parameter sets for a cBot.")]
    public async Task<object> ListParamSets([Description("cBot ID")] Guid cBotId)
    {
        if (CurrentUserId is not { } uid) return Array.Empty<object>();
        var cid = CBotId.From(cBotId);
        return await db.ParamSets.Where(p => p.UserId == uid && p.CBotId == cid)
            .Select(p => new { p.Id, p.Name }).ToListAsync();
    }

    [McpServerTool, Description("List user's trading accounts.")]
    public async Task<object> ListTradingAccounts()
    {
        if (CurrentUserId is not { } uid) return Array.Empty<object>();
        return await db.TradingAccounts.Where(t => t.CTid.UserId == uid)
            .Select(t => new { t.Id, t.AccountNumber, t.Broker, t.IsLive }).ToListAsync();
    }
}
