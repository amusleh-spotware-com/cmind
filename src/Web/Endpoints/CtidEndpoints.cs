using System.Text;
using Core;
using Core.Accounts;
using Core.Constants;
using Core.Domain;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Web.Endpoints;

public record CreateCtidRequest(string Username, string Password);
public record UpdateCtidRequest(string Username, string? Password);
public record CreateTradingAccountRequest(long AccountNumber, string Broker, bool IsLive, string? Label);

public static class CtidEndpoints
{
    public static IEndpointRouteBuilder MapCtidEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/ctids").RequireAuthorization("UserOrAbove")
            .RequireFeature(Core.Features.FeatureFlag.Accounts);

        g.MapGet("/", async (DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            return await db.CTids.Where(c => c.UserId == uid)
                .Select(c => new { c.Id, c.Username, c.CtidUserId }).ToListAsync();
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
            var accounts = await db.TradingAccounts.Where(t => t.CTidId == cid && t.CTid.UserId == uid).ToListAsync();
            return accounts.Select(ToAccountView);
        });

        g.MapPost("/{id:guid}/accounts", async (Guid id, CreateTradingAccountRequest req,
            DataContext db, ICurrentUser u, IOptionsMonitor<AppOptions> options,
            IBrokerVerifier verifier, CancellationToken ct) =>
        {
            var uid = u.UserId!.Value;
            var cid = CtidId.From(id);
            var c = await db.CTids.FirstOrDefaultAsync(x => x.Id == cid && x.UserId == uid, ct);
            if (c is null) return Results.NotFound();

            var allowlist = BrokerAllowlist.FromNames(options.CurrentValue.Accounts.AllowedBrokers);
            var broker = req.Broker;

            // When the deployment restricts brokers, the user-typed broker is not trusted: verify the
            // account's real broker via the cTrader CLI and persist that. Unrestricted ⇒ no probe, any broker.
            if (allowlist.IsRestricted)
            {
                var probe = new BrokerProbeRequest(c.Username, c.EncryptedPassword, req.AccountNumber, req.IsLive);
                var verification = await verifier.VerifyAsync(probe, ct);
                if (!verification.Success || verification.Broker is not { } verified)
                    return Results.Problem(BrokerVerificationMessage(verification.Error),
                        statusCode: StatusCodes.Status422UnprocessableEntity);
                broker = verified.Value;
            }

            try
            {
                c.AddTradingAccount(req.AccountNumber, broker, req.IsLive, req.Label, allowlist);
                await db.SaveChangesAsync(ct);
            }
            catch (DomainException ex) when (ex.Code == DomainErrors.BrokerNotAllowed)
            {
                return Results.Problem($"Accounts from {broker} are not allowed on this deployment.",
                    statusCode: StatusCodes.Status409Conflict);
            }
            return Results.Ok();
        });

        app.MapGet("/api/accounts", async (DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var accounts = await db.TradingAccounts.Where(t => t.CTid.UserId == uid).ToListAsync();
            return accounts.Select(ToAccountView);
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

    private static string BrokerVerificationMessage(BrokerVerificationError error) => error switch
    {
        BrokerVerificationError.LoginFailed =>
            "Couldn't sign in with these cID credentials — check the username, password and account number.",
        BrokerVerificationError.NoNodeAvailable =>
            "No node is available to verify the broker right now — try again shortly.",
        BrokerVerificationError.Timeout =>
            "Verifying the broker timed out — try again shortly.",
        _ => "Couldn't verify the account's broker — please try again."
    };

    private static object ToAccountView(TradingAccount t) => new
    {
        t.Id,
        t.AccountNumber,
        t.Broker,
        t.IsLive,
        t.Label,
        LinkedViaCid = t.LinkMethod.HasFlag(AccountLinkMethod.Cid),
        LinkedViaOpenApi = t.LinkMethod.HasFlag(AccountLinkMethod.OpenApi)
    };
}
