using Core;
using Core.Constants;
using Infrastructure.Persistence;
using Nodes;
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

        g.MapGet("/", async (DataContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            IQueryable<Instance> q = db.Instances.AsNoTracking().Include(i => i.CBot).Include(i => i.Node);
            if (u.IsInRole("Viewer"))
            {
                var user = await db.Users.OfType<ViewerUser>().FirstOrDefaultAsync(x => x.Id == uid);
                if (user is null) return Results.Unauthorized();
                if (!user.SeeAllInstances)
                {
                    var grants = db.ViewerGrants.Where(v => v.ViewerId == uid).Select(v => v.InstanceId);
                    q = q.Where(i => grants.Contains(i.Id));
                }
            }
            else if (u.IsInRole("User"))
            {
                q = q.Where(i => i.UserId == uid);
            }
            var instances = await q.OrderByDescending(i => i.CreatedAt).Take(200).ToListAsync();
            var rows = instances.Select(i => new
            {
                i.Id,
                Kind = i.KindName,
                Status = i.StatusName,
                i.Symbol,
                i.Timeframe,
                CBot = i.CBot.Name,
                Node = i.Node!.Name,
                StartedAt = GetStartedAt(i),
                StoppedAt = GetStoppedAt(i)
            });
            return Results.Ok(rows);
        });

        g.MapGet("/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var iid = InstanceId.From(id);
            var i = await db.Instances.AsNoTracking().FirstOrDefaultAsync(x => x.Id == iid);
            if (i is null) return Results.NotFound();
            if (u.IsInRole("Viewer"))
            {
                var viewer = await db.Users.OfType<ViewerUser>().FirstOrDefaultAsync(x => x.Id == uid);
                if (viewer is null) return Results.Unauthorized();
                if (!viewer.SeeAllInstances && !await db.ViewerGrants.AnyAsync(v => v.ViewerId == uid && v.InstanceId == iid))
                    return Results.Forbid();
            }
            else if (u.IsInRole("User") && i.UserId != uid)
            {
                return Results.Forbid();
            }

            var equity = i is CompletedBacktestInstance completed
                ? ContainerCommandHelpers.ParseEquityCurve(completed.ReportJson)
                    .Select(p => new { p.Timestamp, p.Value })
                : null;

            return Results.Ok(new
            {
                i.Id,
                Kind = i.KindName,
                Status = i.StatusName,
                i.Symbol,
                i.Timeframe,
                Equity = equity
            });
        });

        g.MapPost("/", async (StartRequest req, DataContext db, ICurrentUser u,
            INodeScheduler scheduler, IContainerDispatcherFactory factory, ISecretProtector protector) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (u.IsInRole("Viewer")) return Results.Forbid();

            var cbotId = CBotId.From(req.CBotId);
            var accountId = TradingAccountId.From(req.TradingAccountId);
            var paramSetId = ParamSetId.From(req.ParamSetId);

            var cbot = await db.CBots.FirstOrDefaultAsync(c => c.Id == cbotId && c.UserId == uid);
            if (cbot is null) return Results.BadRequest("cbot not found");
            var acct = await db.TradingAccounts.Include(t => t.CTid)
                .FirstOrDefaultAsync(t => t.Id == accountId && t.CTid.UserId == uid);
            if (acct is null) return Results.BadRequest("account not found");
            var paramSet = await db.ParamSets.FirstOrDefaultAsync(p => p.Id == paramSetId && p.UserId == uid);
            if (paramSet is null) return Results.BadRequest("paramset not found");

            var kind = req.Type;
            var node = await scheduler.PickNodeAsync(kind, default);
            if (node is null) return Results.Conflict("no node available");

            var imageTag = string.IsNullOrWhiteSpace(req.DockerImageTag) ? "latest" : req.DockerImageTag;

            Instance starting;
            if (string.Equals(kind, "Backtest", StringComparison.OrdinalIgnoreCase))
            {
                starting = new StartingBacktestInstance
                {
                    UserId = uid,
                    CBotId = cbotId,
                    TradingAccountId = accountId,
                    NodeId = node.Id,
                    DockerImageTag = imageTag,
                    Symbol = req.Symbol,
                    Timeframe = req.Timeframe,
                    ParamSetId = paramSetId,
                    BacktestSettingsJson = req.BacktestSettingsJson
                };
            }
            else
            {
                starting = new StartingRunInstance
                {
                    UserId = uid,
                    CBotId = cbotId,
                    TradingAccountId = accountId,
                    NodeId = node.Id,
                    DockerImageTag = imageTag,
                    Symbol = req.Symbol,
                    Timeframe = req.Timeframe,
                    ParamSetId = paramSetId
                };
            }
            db.Instances.Add(starting);
            await db.SaveChangesAsync();

            // Re-load node for dispatcher context
            starting.Node = node;

            var algo = protector.Unprotect(cbot.EncryptedAlgo, EncryptionPurposes.CbotAlgo);
            var containerId = await factory.For(node).StartAsync(starting, algo, paramSet.JsonContent, default);

            // Transition to Running by replacing entity (TPH discriminator cannot change)
            db.Instances.Remove(starting);
            Instance running;
            if (starting is StartingBacktestInstance)
            {
                running = new RunningBacktestInstance
                {
                    UserId = uid,
                    CBotId = cbotId,
                    TradingAccountId = accountId,
                    NodeId = node.Id,
                    DockerImageTag = imageTag,
                    Symbol = req.Symbol,
                    Timeframe = req.Timeframe,
                    ParamSetId = paramSetId,
                    BacktestSettingsJson = req.BacktestSettingsJson,
                    ContainerId = containerId,
                    StartedAt = DateTimeOffset.UtcNow
                };
            }
            else
            {
                running = new RunningRunInstance
                {
                    UserId = uid,
                    CBotId = cbotId,
                    TradingAccountId = accountId,
                    NodeId = node.Id,
                    DockerImageTag = imageTag,
                    Symbol = req.Symbol,
                    Timeframe = req.Timeframe,
                    ParamSetId = paramSetId,
                    ContainerId = containerId,
                    StartedAt = DateTimeOffset.UtcNow
                };
            }
            running.DataDirSubPath = starting.DataDirSubPath;
            db.Instances.Add(running);
            await db.SaveChangesAsync();
            return Results.Ok(new { running.Id });
        });

        g.MapPost("/{id:guid}/stop", async (Guid id, DataContext db, ICurrentUser u,
            IContainerDispatcherFactory factory) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var iid = InstanceId.From(id);
            var i = await db.Instances.Include(x => x.Node).FirstOrDefaultAsync(x => x.Id == iid);
            if (i is null) return Results.NotFound();
            if (u.IsInRole("Viewer") || (u.IsInRole("User") && i.UserId != uid))
                return Results.Forbid();

            if (i.Node is not null)
                try { await factory.For(i).StopAsync(i, default); } catch { /* swallow */ }

            // Replace with Stopped/Completed entity
            var now = DateTimeOffset.UtcNow;
            Instance terminal;
            if (i is RunningRunInstance rri)
            {
                terminal = new StoppedRunInstance
                {
                    UserId = i.UserId,
                    CBotId = i.CBotId,
                    TradingAccountId = i.TradingAccountId,
                    NodeId = i.NodeId,
                    DockerImageTag = i.DockerImageTag,
                    Symbol = i.Symbol,
                    Timeframe = i.Timeframe,
                    ParamSetId = i.ParamSetId,
                    ContainerId = rri.ContainerId,
                    StartedAt = rri.StartedAt,
                    StoppedAt = now
                };
            }
            else if (i is RunningBacktestInstance rbi)
            {
                terminal = new CompletedBacktestInstance
                {
                    UserId = i.UserId,
                    CBotId = i.CBotId,
                    TradingAccountId = i.TradingAccountId,
                    NodeId = i.NodeId,
                    DockerImageTag = i.DockerImageTag,
                    Symbol = i.Symbol,
                    Timeframe = i.Timeframe,
                    ParamSetId = i.ParamSetId,
                    BacktestSettingsJson = ((BacktestInstance)i).BacktestSettingsJson,
                    ContainerId = rbi.ContainerId,
                    StartedAt = rbi.StartedAt,
                    StoppedAt = now
                };
            }
            else
            {
                return Results.Ok();
            }
            db.Instances.Remove(i);
            db.Instances.Add(terminal);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapDelete("/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var iid = InstanceId.From(id);
            var i = await db.Instances.FirstOrDefaultAsync(x => x.Id == iid);
            if (i is null) return Results.NotFound();
            if (u.IsInRole("Viewer") || (u.IsInRole("User") && i.UserId != uid))
                return Results.Forbid();
            if (i.IsActive) return Results.Conflict("stop first");
            db.Instances.Remove(i);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    internal static DateTimeOffset? GetStartedAt(Instance i) => i switch
    {
        RunningRunInstance r => r.StartedAt,
        RunningBacktestInstance r => r.StartedAt,
        StoppingRunInstance r => r.StartedAt,
        StoppingBacktestInstance r => r.StartedAt,
        StoppedRunInstance r => r.StartedAt,
        CompletedBacktestInstance r => r.StartedAt,
        FailedRunInstance r => r.StartedAt,
        FailedBacktestInstance r => r.StartedAt,
        _ => null
    };

    internal static DateTimeOffset? GetStoppedAt(Instance i) => i switch
    {
        StoppedRunInstance r => r.StoppedAt,
        CompletedBacktestInstance r => r.StoppedAt,
        FailedRunInstance r => r.StoppedAt,
        FailedBacktestInstance r => r.StoppedAt,
        _ => null
    };
}
