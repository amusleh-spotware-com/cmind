using System.Security.Cryptography;
using System.Text;
using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record CreateMcpKeyRequest(string Label);

public static class McpKeyEndpoints
{
    public static IEndpointRouteBuilder MapMcpKeyEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/mcp-keys").RequireAuthorization();

        g.MapGet("/", async (DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            return await db.McpApiKeys.Where(k => k.UserId == uid && k.RevokedAt == null)
                .Select(k => new { k.Id, k.Label, k.KeyPrefix, k.CreatedAt, k.LastUsedAt }).ToListAsync();
        });

        g.MapPost("/", async (CreateMcpKeyRequest req, DataContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var raw = "mcpk_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            var prefix = raw[..16];
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
            db.McpApiKeys.Add(McpApiKey.Create(uid, prefix, hash, req.Label));
            await db.SaveChangesAsync();
            return Results.Ok(new { token = raw });
        });

        g.MapDelete("/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var kid = McpApiKeyId.From(id);
            var k = await db.McpApiKeys.FirstOrDefaultAsync(x => x.Id == kid && x.UserId == uid);
            if (k is null) return Results.NotFound();
            if (k.RevokedAt is not null) return Results.NoContent();
            k.Revoke();
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
