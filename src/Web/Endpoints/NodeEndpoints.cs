using System.Text;
using Core;
using Core.Constants;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record CreateNodeRequest(
    string Name, string Host, int SshPort, string SshUser,
    string SshPrivateKey, string? SshKeyPassphrase, string Mode,
    string DataDirPath, int MaxInstances);

public static class NodeEndpoints
{
    public static IEndpointRouteBuilder MapNodeEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/nodes").RequireAuthorization("AdminOrAbove");

        g.MapGet("/", async (CtwDbContext db) =>
        {
            var rows = await db.Nodes.Include(n => n.LatestStats)
                .Select(n => new
                {
                    n.Id,
                    n.Name,
                    n.Host,
                    Mode = n.ModeName,
                    Status = n.StatusName,
                    n.MaxInstances,
                    Stats = n.LatestStats
                }).ToListAsync();
            return rows;
        });

        g.MapPost("/", async (CreateNodeRequest req, CtwDbContext db, ISecretProtector p) =>
        {
            Node node = CreateNodeForMode(req.Mode);
            node.Name = req.Name;
            node.Host = req.Host;
            node.SshPort = req.SshPort;
            node.SshUser = req.SshUser;
            node.EncryptedSshKey = p.Protect(Encoding.UTF8.GetBytes(req.SshPrivateKey), EncryptionPurposes.NodeSshKey);
            node.EncryptedSshKeyPassphrase = string.IsNullOrEmpty(req.SshKeyPassphrase)
                ? null
                : p.Protect(Encoding.UTF8.GetBytes(req.SshKeyPassphrase), EncryptionPurposes.NodeSshPassphrase);
            node.DataDirPath = req.DataDirPath;
            node.MaxInstances = req.MaxInstances;
            db.Nodes.Add(node);
            await db.SaveChangesAsync();
            return Results.Ok(new { node.Id });
        });

        g.MapDelete("/{id:guid}", async (Guid id, CtwDbContext db, IContainerDispatcher dispatcher) =>
        {
            var nid = NodeId.From(id);
            var node = await db.Nodes.Include(n => n.LatestStats).FirstOrDefaultAsync(n => n.Id == nid);
            if (node is null) return Results.NotFound();

            // Stop any active instances on this node
            var active = await db.Instances
                .Where(i => i.NodeId == nid && i.IsActive)
                .ToListAsync();
            foreach (var i in active)
            {
                try { await dispatcher.StopAsync(i, default); } catch { /* swallow */ }
                var now = DateTimeOffset.UtcNow;
                Instance? terminal = i switch
                {
                    RunningRunInstance rri => new StoppedRunInstance
                    {
                        UserId = i.UserId, CBotId = i.CBotId, TradingAccountId = i.TradingAccountId,
                        NodeId = i.NodeId, DockerImageTag = i.DockerImageTag, Symbol = i.Symbol,
                        Timeframe = i.Timeframe, ParamSetId = i.ParamSetId,
                        ContainerId = rri.ContainerId, StartedAt = rri.StartedAt, StoppedAt = now
                    },
                    StartingRunInstance sri => new StoppedRunInstance
                    {
                        UserId = i.UserId, CBotId = i.CBotId, TradingAccountId = i.TradingAccountId,
                        NodeId = i.NodeId, DockerImageTag = i.DockerImageTag, Symbol = i.Symbol,
                        Timeframe = i.Timeframe, ParamSetId = i.ParamSetId,
                        ContainerId = sri.ContainerId, StoppedAt = now
                    },
                    RunningBacktestInstance rbi => new CompletedBacktestInstance
                    {
                        UserId = i.UserId, CBotId = i.CBotId, TradingAccountId = i.TradingAccountId,
                        NodeId = i.NodeId, DockerImageTag = i.DockerImageTag, Symbol = i.Symbol,
                        Timeframe = i.Timeframe, ParamSetId = i.ParamSetId,
                        BacktestSettingsJson = ((BacktestInstance)i).BacktestSettingsJson,
                        ContainerId = rbi.ContainerId, StartedAt = rbi.StartedAt, StoppedAt = now
                    },
                    _ => null
                };
                if (terminal is not null)
                {
                    db.Instances.Remove(i);
                    db.Instances.Add(terminal);
                }
            }
            await db.SaveChangesAsync();
            db.Nodes.Remove(node);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/clean-backtest-data", async (Guid id, CtwDbContext db,
            IContainerDispatcher dispatcher) =>
        {
            var nid = NodeId.From(id);
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nid);
            if (node is null) return Results.NotFound();
            await dispatcher.CleanBacktestDataAsync(node, null, default);
            return Results.Ok();
        });

        return app;
    }

    private static Node CreateNodeForMode(string mode) => mode switch
    {
        "Run" => new ActiveRunNode(),
        "Backtest" => new ActiveBacktestNode(),
        "Mixed" => new ActiveMixedNode(),
        _ => throw new ArgumentException($"Invalid node mode: {mode}", nameof(mode))
    };
}
