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
    string Name, string BaseUrl, string ApiSecret, string Mode,
    string DataDirPath, int MaxInstances);

public static class NodeEndpoints
{
    public static IEndpointRouteBuilder MapNodeEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/nodes").RequireAuthorization("AdminOrAbove");

        g.MapGet("/", async (DataContext db) =>
        {
            var rows = await db.Nodes.Include(n => n.LatestStats)
                .Select(n => new
                {
                    n.Id,
                    n.Name,
                    Host = n is RemoteNode ? ((RemoteNode)n).BaseUrl : "local",
                    Mode = n.ModeName,
                    Status = n.StatusName,
                    n.MaxInstances,
                    IsLocal = n is LocalNode,
                    Enabled = n is LocalNode ? ((LocalNode)n).Enabled : (bool?)null,
                    Stats = n.LatestStats == null ? null : new
                    {
                        n.LatestStats.CpuPercent,
                        n.LatestStats.MemUsedBytes,
                        n.LatestStats.MemTotalBytes,
                        n.LatestStats.DiskUsedBytes,
                        n.LatestStats.DiskTotalBytes,
                        n.LatestStats.BacktestDataUsedBytes,
                        n.LatestStats.RunningCount,
                        n.LatestStats.BacktestCount,
                        n.LatestStats.UpdatedAt
                    }
                }).ToListAsync();
            return rows;
        });

        g.MapPost("/", async (CreateNodeRequest req, DataContext db, ISecretProtector p) =>
        {
            if (string.IsNullOrWhiteSpace(req.BaseUrl) || !Uri.TryCreate(req.BaseUrl, UriKind.Absolute, out _))
                return Results.BadRequest("invalid base url");
            if (req.ApiSecret.Length < NodeAgentAuth.MinSecretLength)
                return Results.BadRequest($"api secret must be at least {NodeAgentAuth.MinSecretLength} characters");

            RemoteNode node = CreateNodeForMode(req.Mode);
            node.Name = req.Name;
            node.BaseUrl = req.BaseUrl.TrimEnd('/');
            node.EncryptedApiSecret = p.Protect(Encoding.UTF8.GetBytes(req.ApiSecret), EncryptionPurposes.NodeApiSecret);
            node.DataDirPath = req.DataDirPath;
            node.MaxInstances = req.MaxInstances;
            db.Nodes.Add(node);
            await db.SaveChangesAsync();
            return Results.Ok(new { node.Id });
        });

        g.MapPost("/local/toggle", async (LocalToggleRequest req, DataContext db) =>
        {
            var node = await db.Nodes.OfType<LocalNode>().FirstOrDefaultAsync();
            if (node is null) return Results.NotFound("local node not seeded");
            node.Enabled = req.Enabled;
            node.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { node.Id, node.Enabled });
        });

        g.MapDelete("/{id:guid}", async (Guid id, DataContext db, IContainerDispatcherFactory factory) =>
        {
            var nid = NodeId.From(id);
            var node = await db.Nodes.Include(n => n.LatestStats).FirstOrDefaultAsync(n => n.Id == nid);
            if (node is null) return Results.NotFound();

            // Stop any active instances on this node
            var active = (await db.Instances.Where(i => i.NodeId == nid).ToListAsync())
                .Where(i => i.IsActive)
                .ToList();
            foreach (var i in active)
            {
                try { await factory.For(node).StopAsync(i, default); } catch { /* swallow */ }
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

        g.MapPost("/{id:guid}/clean-backtest-data", async (Guid id, DataContext db,
            IContainerDispatcherFactory factory) =>
        {
            var nid = NodeId.From(id);
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nid);
            if (node is null) return Results.NotFound();
            await factory.For(node).CleanBacktestDataAsync(node, null, default);
            return Results.Ok();
        });

        return app;
    }

    private static RemoteNode CreateNodeForMode(string mode) => mode switch
    {
        "Run" => new ActiveRunNode(),
        "Backtest" => new ActiveBacktestNode(),
        "Mixed" => new ActiveMixedNode(),
        _ => throw new ArgumentException($"Invalid node mode: {mode}", nameof(mode))
    };
}

public record LocalToggleRequest(bool Enabled);
