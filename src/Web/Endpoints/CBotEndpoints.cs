using Core;
using Core.Constants;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public static class CBotEndpoints
{
    private const long MaxAlgoBytes = 10 * 1024 * 1024;

    public static IEndpointRouteBuilder MapCBotEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cbots").RequireAuthorization("UserOrAbove").DisableAntiforgery()
            .RequireFeature(Core.Features.FeatureFlag.Authoring);

        g.MapGet("/", async (DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            return await db.CBots.Where(c => c.UserId == uid)
                .Select(c => new { c.Id, c.Name, c.CreatedAt, c.SourceProjectId, HasSource = c.SourceProjectId != null })
                .ToListAsync();
        });

        g.MapPost("/upload", async (HttpRequest req, DataContext db, ICurrentUser u, ISecretProtector p) =>
        {
            if (!req.HasFormContentType) return Results.BadRequest("multipart required");
            var form = await req.ReadFormAsync();
            var file = form.Files["file"];
            var name = form["name"].ToString();
            if (file is null || file.Length == 0 || file.Length > MaxAlgoBytes)
                return Results.BadRequest("invalid file");
            if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(file.FileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var cbot = CBot.Create(u.UserId!.Value, name, p.Protect(bytes, EncryptionPurposes.CbotAlgo));
            db.CBots.Add(cbot);
            await db.SaveChangesAsync();
            return Results.Ok(new { cbot.Id });
        });

        g.MapPatch("/{id:guid}", async (Guid id, RenameRequest req, DataContext db, ICurrentUser u) =>
        {
            var cid = CBotId.From(id);
            var cbot = await db.CBots.FirstOrDefaultAsync(c => c.Id == cid && c.UserId == u.UserId!.Value);
            if (cbot is null) return Results.NotFound();
            cbot.Rename(req.Name);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapPost("/{id:guid}/quick-run", async (Guid id, DataContext db, ICurrentUser u,
            INodeScheduler scheduler, IContainerDispatcherFactory factory, ISecretProtector protector,
            TimeProvider timeProvider) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var cid = CBotId.From(id);
            var cbot = await db.CBots.FirstOrDefaultAsync(c => c.Id == cid && c.UserId == uid);
            if (cbot is null) return Results.NotFound();

            var node = await scheduler.PickNodeAsync("Run", default);
            if (node is null) return Results.Conflict("no node available");

            var algo = protector.Unprotect(cbot.EncryptedAlgo, EncryptionPurposes.CbotAlgo);
            var starting = RunInstance.CreateStarting(uid, cbot.Id, node.Id, DockerImageTag.Latest,
                new Symbol("EURUSD"), new Timeframe("h1"));
            db.Instances.Add(starting);
            await db.SaveChangesAsync();
            starting.AttachNode(node);

            string containerId;
            try
            {
                containerId = await factory.For(node).StartAsync(starting, algo, "{}", default);
            }
            catch (Exception ex)
            {
                var failed = starting.ToFailed(ex.Message, timeProvider.GetUtcNow());
                db.Instances.Remove(starting);
                db.Instances.Add(failed);
                await db.SaveChangesAsync();
                return Results.Ok(new { success = false, output = ex.Message, instanceId = (Guid?)null });
            }

            var running = starting.ToRunning(containerId, timeProvider.GetUtcNow());
            db.Instances.Remove(starting);
            db.Instances.Add(running);
            await db.SaveChangesAsync();
            return Results.Ok(new { success = true, output = (string?)null, instanceId = (Guid?)running.Id.Value });
        });

        g.MapDelete("/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            var cid = CBotId.From(id);
            var cbot = await db.CBots.FirstOrDefaultAsync(c => c.Id == cid && c.UserId == u.UserId!.Value);
            if (cbot is null) return Results.NotFound();
            db.CBots.Remove(cbot);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}

public record RenameRequest(string Name);
