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

        g.MapGet("/", async (CtwDbContext db, ICurrentUser u) =>
            await db.McpApiKeys.Where(k => k.UserId == u.UserId && k.RevokedAt == null)
                .Select(k => new { k.Id, k.Label, k.KeyPrefix, k.CreatedAt, k.LastUsedAt }).ToListAsync());

        g.MapPost("/", async (CreateMcpKeyRequest req, CtwDbContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var raw = "ctw_mcp_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            var prefix = raw[..16];
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
            db.McpApiKeys.Add(new McpApiKey
            {
                UserId = uid, KeyPrefix = prefix, KeyHash = hash, Label = req.Label
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { token = raw });
        });

        g.MapDelete("/{id:guid}", async (Guid id, CtwDbContext db, ICurrentUser u) =>
        {
            var k = await db.McpApiKeys.FirstOrDefaultAsync(x => x.Id == id && x.UserId == u.UserId);
            if (k is null) return Results.NotFound();
            k.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
