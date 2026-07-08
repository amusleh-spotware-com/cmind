using Core;
using Core.Constants;
using Infrastructure.Persistence;
using Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public static class PropGuardEndpoints
{
    public static IEndpointRouteBuilder MapPropGuardEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/prop").RequireAuthorization(AuthPolicies.UserOrAbove);

        g.MapGet("/rules", async (DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var rules = await db.PropRules.Where(r => r.UserId == uid)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    id = r.Id.Value,
                    name = r.Name,
                    tradingAccountId = r.TradingAccountId.Value,
                    accountNumber = r.TradingAccount.AccountNumber,
                    broker = r.TradingAccount.Broker,
                    maxConcurrentLiveInstances = r.MaxConcurrentLiveInstances,
                    dailyLossLimit = r.DailyLossLimit,
                    maxDrawdownPercent = r.MaxDrawdownPercent,
                    autoFlatten = r.AutoFlatten,
                    enabled = r.Enabled,
                    lastFlattenedAt = r.LastFlattenedAt
                })
                .ToListAsync(ct);
            return Results.Ok(rules);
        });

        g.MapPost("/rules", async (CreatePropRuleRequest req, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("name required");

            var accountId = TradingAccountId.From(req.TradingAccountId);
            var ownsAccount = await db.TradingAccounts.Include(t => t.CTid)
                .AnyAsync(t => t.Id == accountId && t.CTid.UserId == uid, ct);
            if (!ownsAccount) return Results.BadRequest("trading account not found");

            var rule = new PropRule
            {
                UserId = uid,
                TradingAccountId = accountId,
                Name = req.Name!.Trim(),
                MaxConcurrentLiveInstances = Math.Clamp(req.MaxConcurrentLiveInstances ?? 3, 0, PropGuardConstants.MaxConcurrentCap),
                DailyLossLimit = req.DailyLossLimit ?? 0,
                MaxDrawdownPercent = req.MaxDrawdownPercent ?? 0,
                AutoFlatten = req.AutoFlatten ?? false,
                Enabled = req.Enabled ?? true
            };
            db.PropRules.Add(rule);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return Results.Conflict("a rule already exists for that account"); }
            return Results.Ok(new { id = rule.Id.Value });
        });

        g.MapPut("/rules/{id:guid}", async (Guid id, UpdatePropRuleRequest req, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var rid = PropRuleId.From(id);
            var rule = await db.PropRules.FirstOrDefaultAsync(r => r.Id == rid && r.UserId == uid, ct);
            if (rule is null) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(req.Name)) rule.Name = req.Name!.Trim();
            if (req.MaxConcurrentLiveInstances is { } max) rule.MaxConcurrentLiveInstances = Math.Clamp(max, 0, PropGuardConstants.MaxConcurrentCap);
            if (req.DailyLossLimit is { } dll) rule.DailyLossLimit = dll;
            if (req.MaxDrawdownPercent is { } dd) rule.MaxDrawdownPercent = dd;
            if (req.AutoFlatten is { } af) rule.AutoFlatten = af;
            if (req.Enabled is { } en) rule.Enabled = en;
            rule.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        });

        g.MapDelete("/rules/{id:guid}", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var rid = PropRuleId.From(id);
            var rule = await db.PropRules.FirstOrDefaultAsync(r => r.Id == rid && r.UserId == uid, ct);
            if (rule is null) return Results.NotFound();
            db.PropRules.Remove(rule);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/flatten/{accountId:guid}", async (
            Guid accountId, DataContext db, ICurrentUser u, IContainerDispatcherFactory factory, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var aid = TradingAccountId.From(accountId);
            var ownsAccount = await db.TradingAccounts.Include(t => t.CTid)
                .AnyAsync(t => t.Id == aid && t.CTid.UserId == uid, ct);
            if (!ownsAccount) return Results.NotFound();

            var live = await db.Instances.OfType<RunningRunInstance>()
                .Include(i => i.Node)
                .Where(i => i.UserId == uid && i.TradingAccountId == aid)
                .ToListAsync(ct);

            var now = DateTimeOffset.UtcNow;
            foreach (var instance in live)
            {
                if (instance.Node is not null)
                    try { await factory.For(instance).StopAsync(instance, ct); }
                    catch { /* best effort */ }
                db.Instances.Remove(instance);
                db.Instances.Add(InstanceTransitions.StoppedFrom(instance, now));
            }
            if (live.Count > 0) await db.SaveChangesAsync(ct);
            return Results.Ok(new { flattened = live.Count });
        });

        return app;
    }
}

public sealed record CreatePropRuleRequest(
    Guid TradingAccountId, string? Name, int? MaxConcurrentLiveInstances,
    double? DailyLossLimit, double? MaxDrawdownPercent, bool? AutoFlatten, bool? Enabled);

public sealed record UpdatePropRuleRequest(
    string? Name, int? MaxConcurrentLiveInstances, double? DailyLossLimit,
    double? MaxDrawdownPercent, bool? AutoFlatten, bool? Enabled);
