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
        var g = app.MapGroup("/api/paramsets").RequireAuthorization("UserOrAbove")
            .RequireFeature(Core.Features.FeatureFlag.Authoring);

        g.MapGet("/", async (Guid? cbotId, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var q = db.ParamSets.Where(p => p.UserId == uid);
            if (cbotId is { } cid)
            {
                var c = CBotId.From(cid);
                q = q.Where(p => p.CBotId == c);
            }
            return await q.Select(p => new
            {
                p.Id,
                p.Name,
                p.CBotId,
                CBotName = db.CBots.Where(c => c.Id == p.CBotId).Select(c => c.Name).FirstOrDefault()
            }).ToListAsync();
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
            var name = req.Name?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest("name is required");
            if (!ParamSetJson.IsValidSchema(req.JsonContent))
                return Results.BadRequest("parameters must be a flat JSON object mapping each parameter name to a scalar value");
            var cbot = CBotId.From(req.CBotId);
            var lowered = name.ToLowerInvariant();
            if (await db.ParamSets.AnyAsync(p => p.UserId == uid && p.CBotId == cbot && p.Name.ToLower() == lowered))
                return Results.Conflict($"a parameter set named \"{name}\" already exists for this cBot");
            db.ParamSets.Add(ParamSet.Create(uid, cbot, name, req.JsonContent));
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapPut("/{id:guid}", async (Guid id, UpdateParamSetRequest req, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var pid = ParamSetId.From(id);
            var p = await db.ParamSets.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            if (p is null) return Results.NotFound();
            var name = req.Name?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest("name is required");
            if (!ParamSetJson.IsValidSchema(req.JsonContent))
                return Results.BadRequest("parameters must be a flat JSON object mapping each parameter name to a scalar value");
            var lowered = name.ToLowerInvariant();
            if (await db.ParamSets.AnyAsync(x => x.UserId == uid && x.CBotId == p.CBotId && x.Name.ToLower() == lowered && x.Id != pid))
                return Results.Conflict($"a parameter set named \"{name}\" already exists for this cBot");
            p.Update(name, req.JsonContent);
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
}
