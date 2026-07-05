using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record CreateParamSetRequest(Guid CBotId, string Name, string JsonContent);
public record UpdateParamSetRequest(Guid CBotId, string Name, string JsonContent);

public static class ParamSetEndpoints
{
    public static IEndpointRouteBuilder MapParamSetEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/paramsets").RequireAuthorization("UserOrAbove");

        g.MapGet("/", async (Guid? cbotId, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var q = db.ParamSets.Where(p => p.UserId == uid);
            if (cbotId is { } cid)
            {
                var c = CBotId.From(cid);
                q = q.Where(p => p.CBotId == c);
            }
            return await q.Select(p => new { p.Id, p.Name, p.CBotId }).ToListAsync();
        });

        g.MapGet("/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var pid = ParamSetId.From(id);
            var p = await db.ParamSets.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            return p is null ? Results.NotFound() : Results.Ok(p);
        });

        g.MapPost("/", async (CreateParamSetRequest req, DataContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("name is required");
            if (!IsNonEmptyJson(req.JsonContent)) return Results.BadRequest("content cannot be empty");
            db.ParamSets.Add(new ParamSet
            {
                UserId = uid,
                CBotId = CBotId.From(req.CBotId),
                Name = req.Name,
                JsonContent = req.JsonContent
            });
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapPut("/{id:guid}", async (Guid id, UpdateParamSetRequest req, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var pid = ParamSetId.From(id);
            var p = await db.ParamSets.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            if (p is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("name is required");
            if (!IsNonEmptyJson(req.JsonContent)) return Results.BadRequest("content cannot be empty");
            p.Name = req.Name;
            p.JsonContent = req.JsonContent;
            p.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapDelete("/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var pid = ParamSetId.From(id);
            var p = await db.ParamSets.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            if (p is null) return Results.NotFound();
            db.ParamSets.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private static bool IsNonEmptyJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        System.Text.Json.JsonDocument doc;
        try { doc = System.Text.Json.JsonDocument.Parse(content); }
        catch { return false; }
        using (doc)
        {
            return doc.RootElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Object => doc.RootElement.EnumerateObject().Any(),
                System.Text.Json.JsonValueKind.Array => doc.RootElement.EnumerateArray().Any(),
                _ => true
            };
        }
    }
}
