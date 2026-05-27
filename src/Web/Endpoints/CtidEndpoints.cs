using System.Text;
using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record CreateCtidRequest(string Username, string Password);
public record UpdateCtidRequest(string Username, string? Password);
public record CreateTradingAccountRequest(long AccountNumber, string Broker, bool IsLive, string? Label);

public static class CtidEndpoints
{
    public static IEndpointRouteBuilder MapCtidEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/ctids").RequireAuthorization("UserOrAbove");

        g.MapGet("/", async (CtwDbContext db, ICurrentUser u) =>
            await db.CTids.Where(c => c.UserId == u.UserId)
                .Select(c => new { c.Id, c.Username }).ToListAsync());

        g.MapPost("/", async (CreateCtidRequest req, CtwDbContext db, ICurrentUser u, ISecretProtector p) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            db.CTids.Add(new CTraderIdAccount
            {
                UserId = uid,
                Username = req.Username,
                EncryptedPassword = p.Protect(Encoding.UTF8.GetBytes(req.Password), "ctid.password")
            });
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapPut("/{id:guid}", async (Guid id, UpdateCtidRequest req,
            CtwDbContext db, ICurrentUser u, ISecretProtector p) =>
        {
            var c = await db.CTids.FirstOrDefaultAsync(x => x.Id == id && x.UserId == u.UserId);
            if (c is null) return Results.NotFound();
            c.Username = req.Username;
            if (!string.IsNullOrEmpty(req.Password))
                c.EncryptedPassword = p.Protect(Encoding.UTF8.GetBytes(req.Password), "ctid.password");
            c.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapDelete("/{id:guid}", async (Guid id, CtwDbContext db, ICurrentUser u) =>
        {
            var c = await db.CTids.FirstOrDefaultAsync(x => x.Id == id && x.UserId == u.UserId);
            if (c is null) return Results.NotFound();
            db.CTids.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapGet("/{id:guid}/accounts", async (Guid id, CtwDbContext db, ICurrentUser u) =>
            await db.TradingAccounts.Where(t => t.CTidId == id && t.CTid.UserId == u.UserId)
                .Select(t => new { t.Id, t.AccountNumber, t.Broker, t.IsLive, t.Label }).ToListAsync());

        g.MapPost("/{id:guid}/accounts", async (Guid id, CreateTradingAccountRequest req,
            CtwDbContext db, ICurrentUser u) =>
        {
            var c = await db.CTids.FirstOrDefaultAsync(x => x.Id == id && x.UserId == u.UserId);
            if (c is null) return Results.NotFound();
            db.TradingAccounts.Add(new TradingAccount
            {
                CTidId = id,
                AccountNumber = req.AccountNumber,
                Broker = req.Broker,
                IsLive = req.IsLive,
                Label = req.Label
            });
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        app.MapGet("/api/accounts", async (CtwDbContext db, ICurrentUser u) =>
            await db.TradingAccounts.Where(t => t.CTid.UserId == u.UserId)
                .Select(t => new { t.Id, t.AccountNumber, t.Broker, t.IsLive, t.Label }).ToListAsync())
            .RequireAuthorization("UserOrAbove");

        app.MapDelete("/api/accounts/{id:guid}", async (Guid id, CtwDbContext db, ICurrentUser u) =>
        {
            var t = await db.TradingAccounts.Include(x => x.CTid).FirstOrDefaultAsync(x => x.Id == id);
            if (t is null || t.CTid.UserId != u.UserId) return Results.NotFound();
            db.TradingAccounts.Remove(t);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("UserOrAbove");

        return app;
    }
}
