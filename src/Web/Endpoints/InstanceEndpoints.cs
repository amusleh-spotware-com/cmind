using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record StartRequest(
    Guid CBotId, Guid TradingAccountId, string Symbol, string Timeframe,
    Guid ParamSetId, string DockerImageTag, string Type,
    string? BacktestSettingsJson);

public static class InstanceEndpoints
{
    public static IEndpointRouteBuilder MapInstanceEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/instances").RequireAuthorization();

        g.MapGet("/", async (CtwDbContext db, ICurrentUser u) =>
        {
            IQueryable<Instance> q = db.Instances.Include(i => i.CBot).Include(i => i.Node);
            if (u.Role == UserRole.Viewer)
            {
                var user = await db.Users.FindAsync(u.UserId);
                if (user is null) return Results.Unauthorized();
                if (!user.ViewerSeeAllInstances)
                {
                    var grants = db.ViewerGrants.Where(v => v.ViewerId == u.UserId).Select(v => v.InstanceId);
                    q = q.Where(i => grants.Contains(i.Id));
                }
            }
            else if (u.Role == UserRole.User)
            {
                q = q.Where(i => i.UserId == u.UserId);
            }
            var raw = await q.OrderByDescending(i => i.CreatedAt).Take(200)
                .Select(i => new { i.Id, i.Type, i.Status, i.Symbol, i.Timeframe,
                    CBot = i.CBot.Name, Node = i.Node!.Name, i.StartedAt, i.StoppedAt })
                .ToListAsync();
            var rows = raw.Select(i => new { i.Id, Type = i.Type.Name, Status = i.Status.Name,
                i.Symbol, i.Timeframe, i.CBot, i.Node, i.StartedAt, i.StoppedAt }).ToList();
            return Results.Ok(rows);
        });

        g.MapPost("/", async (StartRequest req, CtwDbContext db, ICurrentUser u,
            INodeScheduler scheduler, IContainerDispatcher dispatcher, ISecretProtector protector) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (u.Role?.Name == UserRole.Viewer.Name) return Results.Forbid();

            var cbot = await db.CBots.FirstOrDefaultAsync(c => c.Id == req.CBotId && c.UserId == uid);
            if (cbot is null) return Results.BadRequest("cbot not found");
            var acct = await db.TradingAccounts.Include(t => t.CTid)
                .FirstOrDefaultAsync(t => t.Id == req.TradingAccountId && t.CTid.UserId == uid);
            if (acct is null) return Results.BadRequest("account not found");
            var paramSet = await db.ParamSets.FirstOrDefaultAsync(p => p.Id == req.ParamSetId && p.UserId == uid);
            if (paramSet is null) return Results.BadRequest("paramset not found");

            var type = InstanceType.FromName(req.Type);
            var node = await scheduler.PickNodeAsync(type, default);
            if (node is null) return Results.Conflict("no node available");

            var instance = new Instance
            {
                UserId = uid,
                CBotId = req.CBotId,
                TradingAccountId = req.TradingAccountId,
                TradingAccount = acct,
                NodeId = node.Id,
                Node = node,
                Type = type,
                Status = InstanceStatus.Starting,
                DockerImageTag = string.IsNullOrWhiteSpace(req.DockerImageTag) ? "latest" : req.DockerImageTag,
                Symbol = req.Symbol,
                Timeframe = req.Timeframe,
                ParamSetId = req.ParamSetId,
                BacktestSettingsJson = req.BacktestSettingsJson,
                StartedAt = DateTimeOffset.UtcNow
            };
            db.Instances.Add(instance);
            await db.SaveChangesAsync();

            var algo = protector.Unprotect(cbot.EncryptedAlgo, "cbot.algo");
            instance.ContainerId = await dispatcher.StartAsync(instance, algo, paramSet.JsonContent, default);
            instance.Status = InstanceStatus.Running;
            await db.SaveChangesAsync();
            return Results.Ok(new { instance.Id });
        });

        g.MapPost("/{id:guid}/stop", async (Guid id, CtwDbContext db, ICurrentUser u,
            IContainerDispatcher dispatcher) =>
        {
            var i = await db.Instances.Include(x => x.Node).FirstOrDefaultAsync(x => x.Id == id);
            if (i is null) return Results.NotFound();
            if (u.Role == UserRole.Viewer || (u.Role == UserRole.User && i.UserId != u.UserId))
                return Results.Forbid();
            i.Status = InstanceStatus.Stopping;
            await db.SaveChangesAsync();
            await dispatcher.StopAsync(i, default);
            i.Status = InstanceStatus.Stopped;
            i.StoppedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapDelete("/{id:guid}", async (Guid id, CtwDbContext db, ICurrentUser u) =>
        {
            var i = await db.Instances.FirstOrDefaultAsync(x => x.Id == id);
            if (i is null) return Results.NotFound();
            if (u.Role == UserRole.Viewer || (u.Role == UserRole.User && i.UserId != u.UserId))
                return Results.Forbid();
            if (i.Status == InstanceStatus.Running) return Results.Conflict("stop first");
            db.Instances.Remove(i);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
