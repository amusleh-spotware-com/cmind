using System.Security.Cryptography;
using System.Text;
using Core;
using Core.Constants;
using Core.Domain;
using Core.Logging;
using Core.NodeAgent;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Web.Endpoints;

public record CreateNodeRequest(
    string Name, string BaseUrl, string ApiSecret, string Mode,
    string DataDirPath, int MaxInstances);

public static class NodeEndpoints
{
    public static IEndpointRouteBuilder MapNodeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(NodeDiscoveryRoutes.Register, RegisterNodeAsync).AllowAnonymous();

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

            var node = RemoteNode.Create(NodeMode.FromName(req.Mode), req.Name, req.BaseUrl,
                p.Protect(Encoding.UTF8.GetBytes(req.ApiSecret), EncryptionPurposes.NodeApiSecret),
                req.DataDirPath, req.MaxInstances);
            db.Nodes.Add(node);
            await db.SaveChangesAsync();
            return Results.Ok(new { node.Id });
        });

        g.MapPost("/local/toggle", async (LocalToggleRequest req, DataContext db) =>
        {
            var node = await db.Nodes.OfType<LocalNode>().FirstOrDefaultAsync();
            if (node is null) return Results.NotFound("local node not seeded");
            node.SetEnabled(req.Enabled);
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
                    RunningRunInstance rri => rri.ToStopped(now),
                    StartingRunInstance sri => sri.ToStopped(now),
                    RunningBacktestInstance rbi => rbi.ToCompleted(now),
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

    private static async Task<IResult> RegisterNodeAsync(
        NodeRegistrationRequest req,
        HttpContext ctx,
        DataContext db,
        ISecretProtector protector,
        IOptionsMonitor<AppOptions> options,
        ILoggerFactory loggerFactory)
    {
        var discovery = options.CurrentValue.Discovery;
        if (!discovery.Enabled || string.IsNullOrWhiteSpace(discovery.JoinToken))
            return Results.NotFound();

        var presented = ExtractBearer(ctx);
        if (presented is null || !TokensMatch(presented, discovery.JoinToken))
            return Results.Unauthorized();

        if (req.ProtocolVersion != NodeAgentProtocol.Version)
            return Results.Problem(
                $"Protocol version mismatch: main speaks {NodeAgentProtocol.Version}, agent sent {req.ProtocolVersion}.",
                statusCode: StatusCodes.Status426UpgradeRequired);

        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest("name required");

        NodeEndpointUrl endpoint;
        NodeMode mode;
        try
        {
            endpoint = new NodeEndpointUrl(req.BaseUrl);
            mode = NodeMode.FromName(req.Mode);
        }
        catch (Exception ex) when (ex is DomainException or ArgumentException)
        {
            return Results.BadRequest(ex.Message);
        }

        var maxInstances = req.MaxInstances <= 0 ? DefaultMaxInstances : req.MaxInstances;
        var dataDir = string.IsNullOrWhiteSpace(req.DataDirPath) ? FilePaths.NodeDataDirDefault : req.DataDirPath;
        var heartbeatSeconds = (int)discovery.HeartbeatInterval.TotalSeconds;
        var log = loggerFactory.CreateLogger(nameof(NodeEndpoints));

        var existingNode = await db.Nodes.FirstOrDefaultAsync(n => n.Name == req.Name);
        if (existingNode is RemoteNode existing)
        {
            if (!string.Equals(existing.ModeName, mode.Name, StringComparison.Ordinal))
                log.NodeModeChangeIgnored(existing.Name, mode.Name, existing.ModeName);
            existing.RecordHeartbeat(endpoint, maxInstances);
            await db.SaveChangesAsync();
            return Results.Ok(new NodeRegistrationResponse(existing.Id.Value, heartbeatSeconds));
        }
        if (existingNode is not null)
            return Results.Conflict("node name already in use by a non-remote node");

        var secret = protector.Protect(Encoding.UTF8.GetBytes(discovery.JoinToken), EncryptionPurposes.NodeApiSecret);
        var node = RemoteNode.SelfRegister(mode, req.Name, endpoint, secret, dataDir, maxInstances);
        db.Nodes.Add(node);
        await db.SaveChangesAsync();
        log.NodeSelfRegistered(node.Name, endpoint.Value);
        return Results.Ok(new NodeRegistrationResponse(node.Id.Value, heartbeatSeconds));
    }

    private static string? ExtractBearer(HttpContext ctx)
    {
        var header = ctx.Request.Headers.Authorization.ToString();
        var prefix = $"{AuthSchemes.Bearer} ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : null;
    }

    private static bool TokensMatch(string presented, string expected)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(expected));

    private const int DefaultMaxInstances = 10;
}

public record LocalToggleRequest(bool Enabled);
