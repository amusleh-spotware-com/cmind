using System.Text;
using Core;
using Core.Constants;
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

        g.MapGet("/", async (DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            return await db.CTids.Where(c => c.UserId == uid)
                .Select(c => new { c.Id, c.Username }).ToListAsync();
        });

        g.MapPost("/", async (CreateCtidRequest req, DataContext db, ICurrentUser u, ISecretProtector p) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            db.CTids.Add(CTraderIdAccount.Create(uid, req.Username,
                p.Protect(Encoding.UTF8.GetBytes(req.Password), EncryptionPurposes.CtidPassword)));
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapPut("/{id:guid}", async (Guid id, UpdateCtidRequest req,
            DataContext db, ICurrentUser u, ISecretProtector p) =>
        {
            var uid = u.UserId!.Value;
            var cid = CtidId.From(id);
            var c = await db.CTids.FirstOrDefaultAsync(x => x.Id == cid && x.UserId == uid);
            if (c is null) return Results.NotFound();
            c.UpdateUsername(req.Username);
            if (!string.IsNullOrEmpty(req.Password))
                c.UpdatePassword(p.Protect(Encoding.UTF8.GetBytes(req.Password), EncryptionPurposes.CtidPassword));
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapDelete("/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var cid = CtidId.From(id);
            var c = await db.CTids.FirstOrDefaultAsync(x => x.Id == cid && x.UserId == uid);
            if (c is null) return Results.NotFound();
            db.CTids.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapGet("/{id:guid}/accounts", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var cid = CtidId.From(id);
            return await db.TradingAccounts.Where(t => t.CTidId == cid && t.CTid.UserId == uid)
                .Select(t => new { t.Id, t.AccountNumber, t.Broker, t.IsLive, t.Label }).ToListAsync();
        });

        g.MapPost("/{id:guid}/accounts", async (Guid id, CreateTradingAccountRequest req,
            DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var cid = CtidId.From(id);
            var c = await db.CTids.FirstOrDefaultAsync(x => x.Id == cid && x.UserId == uid);
            if (c is null) return Results.NotFound();
            c.AddTradingAccount(req.AccountNumber, req.Broker, req.IsLive, req.Label);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        app.MapGet("/api/accounts", async (DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            return await db.TradingAccounts.Where(t => t.CTid.UserId == uid)
                .Select(t => new { t.Id, t.AccountNumber, t.Broker, t.IsLive, t.Label }).ToListAsync();
        }).RequireAuthorization("UserOrAbove");

        app.MapDelete("/api/accounts/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var tid = TradingAccountId.From(id);
            var t = await db.TradingAccounts.Include(x => x.CTid).FirstOrDefaultAsync(x => x.Id == tid);
            if (t is null || t.CTid.UserId != uid) return Results.NotFound();
            db.TradingAccounts.Remove(t);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("UserOrAbove");

        return app;
    }
}
