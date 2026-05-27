using Core;
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
        var g = app.MapGroup("/api/cbots").RequireAuthorization("UserOrAbove").DisableAntiforgery();

        g.MapGet("/", async (CtwDbContext db, ICurrentUser u) =>
            await db.CBots.Where(c => c.UserId == u.UserId!.Value)
                .Select(c => new { c.Id, c.Name, c.Version, c.CreatedAt, HasSource = c.SourceProjectId != null })
                .ToListAsync());

        g.MapPost("/upload", async (HttpRequest req, CtwDbContext db, ICurrentUser u, ISecretProtector p) =>
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

            var cbot = new CBot
            {
                UserId = u.UserId!.Value,
                Name = name,
                EncryptedAlgo = p.Protect(bytes, "cbot.algo")
            };
            db.CBots.Add(cbot);
            await db.SaveChangesAsync();
            return Results.Ok(new { cbot.Id });
        });

        g.MapPatch("/{id:guid}", async (Guid id, RenameRequest req, CtwDbContext db, ICurrentUser u) =>
        {
            var cbot = await db.CBots.FirstOrDefaultAsync(c => c.Id == id && c.UserId == u.UserId);
            if (cbot is null) return Results.NotFound();
            cbot.Name = req.Name;
            cbot.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapDelete("/{id:guid}", async (Guid id, CtwDbContext db, ICurrentUser u) =>
        {
            var cbot = await db.CBots.FirstOrDefaultAsync(c => c.Id == id && c.UserId == u.UserId);
            if (cbot is null) return Results.NotFound();
            db.CBots.Remove(cbot);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}

public record RenameRequest(string Name);
