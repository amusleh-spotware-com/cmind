using System.Text;
using Core;
using Core.Accounts;
using Core.Constants;
using Core.Domain;
using Core.Options;
using CTraderOpenApi.Auth;
using CTraderOpenApi.Client;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Web.OpenApi;

/// <summary>Outcome of linking an OAuth grant: how many accounts were linked and which were skipped
/// because their broker is not on the deployment's <see cref="BrokerAllowlist"/>.</summary>
public sealed record OpenApiLinkResult(int Linked, IReadOnlyList<string> SkippedBrokers);

/// <summary>
/// Application service that turns an OAuth token grant into persisted domain state: one
/// <see cref="OpenApiAuthorization"/> per cID grant (token authority), a token-only
/// <see cref="CTraderIdAccount"/> for grouping, and a linked (or merged) <see cref="TradingAccount"/>
/// per discovered account. Each aggregate is mutated in its own transaction.
/// </summary>
public sealed class OpenApiAccountLinker(
    DataContext db, ISecretProtector protector, TimeProvider timeProvider, IOptionsMonitor<AppOptions> options)
{
    private const string DefaultBroker = "cTrader";

    public async Task<OpenApiLinkResult> LinkAsync(
        UserId userId,
        OpenApiApplication application,
        OpenApiGrant grant,
        OpenApiTokenResponse tokens,
        CancellationToken ct)
    {
        var allowlist = BrokerAllowlist.FromNames(options.CurrentValue.Accounts.AllowedBrokers);
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

        var linked = 0;
        var skipped = new List<string>();
        foreach (var account in grant.Accounts)
        {
            var broker = string.IsNullOrWhiteSpace(account.Broker) ? DefaultBroker : account.Broker!;

            // The Open API reports the broker authoritatively, so a disallowed account is skipped (not
            // linked) — allowed accounts in the same grant still link. Empty allowlist ⇒ everything links.
            if (allowlist.IsRestricted && !allowlist.Allows(new BrokerName(broker)))
            {
                skipped.Add(broker);
                continue;
            }

            var existing = await db.TradingAccounts.Include(t => t.CTid)
                .FirstOrDefaultAsync(t => t.CTid.UserId == userId && t.AccountNumber == account.TraderLogin, ct);

            var root = existing is not null && existing.CTidId != grantCid.Id
                ? await db.CTids.Include(c => c.TradingAccounts).FirstAsync(c => c.Id == existing.CTidId, ct)
                : grantCid;

            root.LinkOpenApiAccount(
                account.TraderLogin,
                broker,
                account.IsLive,
                new CtidTraderAccountId(account.CtidTraderAccountId),
                authorization.Id,
                label: null,
                allowlist);

            await db.SaveChangesAsync(ct);
            linked++;
        }

        return new OpenApiLinkResult(linked, skipped);
    }
}
