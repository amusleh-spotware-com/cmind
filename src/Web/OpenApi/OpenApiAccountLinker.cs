using System.Text;
using Core;
using Core.Constants;
using Core.Domain;
using CTraderOpenApi.Auth;
using CTraderOpenApi.Client;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Web.OpenApi;

/// <summary>
/// Application service that turns an OAuth token grant into persisted domain state: one
/// <see cref="OpenApiAuthorization"/> per cID grant (token authority), a token-only
/// <see cref="CTraderIdAccount"/> for grouping, and a linked (or merged) <see cref="TradingAccount"/>
/// per discovered account. Each aggregate is mutated in its own transaction.
/// </summary>
public sealed class OpenApiAccountLinker(DataContext db, ISecretProtector protector, TimeProvider timeProvider)
{
    private const string DefaultBroker = "cTrader";

    public async Task LinkAsync(
        UserId userId,
        OpenApiApplication application,
        OpenApiGrant grant,
        OpenApiTokenResponse tokens,
        CancellationToken ct)
    {
        var encryptedAccess = protector.Protect(
            Encoding.UTF8.GetBytes(tokens.AccessToken), EncryptionPurposes.OpenApiAccessToken);
        var encryptedRefresh = protector.Protect(
            Encoding.UTF8.GetBytes(tokens.RefreshToken), EncryptionPurposes.OpenApiRefreshToken);
        var now = timeProvider.GetUtcNow();
        var expiry = now.AddSeconds(tokens.ExpiresInSeconds);
        var isLive = grant.Accounts.Count == 0 || grant.Accounts.Any(a => a.IsLive);

        var authorization = await db.OpenApiAuthorizations
            .FirstOrDefaultAsync(a => a.UserId == userId && a.CtidUserId == grant.CtidUserId, ct);
        if (authorization is null)
        {
            authorization = OpenApiAuthorization.Create(userId, application.Id, new CtidUserId(grant.CtidUserId),
                isLive, encryptedAccess, encryptedRefresh, expiry, OpenApiScope.Trade);
            db.OpenApiAuthorizations.Add(authorization);
        }
        else
        {
            authorization.Refresh(encryptedAccess, encryptedRefresh, expiry, now);
        }

        await db.SaveChangesAsync(ct);

        var grantCid = await db.CTids.Include(c => c.TradingAccounts)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CtidUserId == grant.CtidUserId, ct);
        if (grantCid is null)
        {
            grantCid = CTraderIdAccount.CreateForOpenApi(
                userId, new CtidUserId(grant.CtidUserId), $"cID {grant.CtidUserId}");
            db.CTids.Add(grantCid);
            await db.SaveChangesAsync(ct);
        }

        foreach (var account in grant.Accounts)
        {
            var existing = await db.TradingAccounts.Include(t => t.CTid)
                .FirstOrDefaultAsync(t => t.CTid.UserId == userId && t.AccountNumber == account.TraderLogin, ct);

            var root = existing is not null && existing.CTidId != grantCid.Id
                ? await db.CTids.Include(c => c.TradingAccounts).FirstAsync(c => c.Id == existing.CTidId, ct)
                : grantCid;

            root.LinkOpenApiAccount(
                account.TraderLogin,
                string.IsNullOrWhiteSpace(account.Broker) ? DefaultBroker : account.Broker!,
                account.IsLive,
                new CtidTraderAccountId(account.CtidTraderAccountId),
                authorization.Id,
                label: null);

            await db.SaveChangesAsync(ct);
        }
    }
}
