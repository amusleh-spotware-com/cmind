using System.Text;
using Core;
using Core.Constants;
using Core.CopyTrading;
using CTraderOpenApi.Client;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nodes.CopyTrading;

// Infrastructure adapter for ICopyEquityReader: resolves a destination account's Open API credentials,
// opens a session and computes its current equity (balance + floating P&L via PropFirmEquityCalculator,
// the same calc the copy host uses for equity-proportional sizing). Returns null when the account is not
// Open API linked/authorized. Used only by the fee settlement service, off the trading hot path.
public sealed class OpenApiCopyEquityReader(
    IServiceScopeFactory scopeFactory,
    IOpenApiTradingSessionFactory sessionFactory) : ICopyEquityReader
{
    private readonly PropFirmEquityCalculator _equity = new();

    public async Task<double?> ReadEquityAsync(long destinationCtidTraderAccountId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        var account = await db.TradingAccounts
            .FirstOrDefaultAsync(a => a.CtidTraderAccountId == destinationCtidTraderAccountId, ct);
        if (account?.OpenApiAuthorizationId is null) return null;

        var auth = await db.OpenApiAuthorizations.FirstOrDefaultAsync(a => a.Id == account.OpenApiAuthorizationId, ct);
        if (auth is null) return null;
        var application = await db.OpenApiApplications.FirstOrDefaultAsync(a => a.Id == auth.ApplicationId, ct);
        if (application is null) return null;

        var clientSecret = Encoding.UTF8.GetString(
            protector.Unprotect(application.EncryptedClientSecret, EncryptionPurposes.OpenApiClientSecret));
        var token = Encoding.UTF8.GetString(
            protector.Unprotect(auth.EncryptedAccessToken, EncryptionPurposes.OpenApiAccessToken));
        if (string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(token)) return null;

        await using var session = sessionFactory.Create(account.IsLive, application.ClientId, clientSecret);
        session.AttachAccount(destinationCtidTraderAccountId, token);
        await session.StartAsync(ct);

        var balance = await session.LoadBalanceAsync(destinationCtidTraderAccountId, ct);
        var valuations = await session.LoadPositionValuationsAsync(destinationCtidTraderAccountId, ct);
        if (valuations.Count == 0) return balance;

        var pricing = new Dictionary<long, SymbolPricing>();
        foreach (var symbolId in valuations.Select(v => v.SymbolId).Distinct())
        {
            var (bid, ask) = await session.LoadSpotPriceAsync(destinationCtidTraderAccountId, symbolId, ct);
            pricing[symbolId] = new SymbolPricing(symbolId, bid, ask);
        }
        return _equity.Compute(balance, valuations, pricing).Equity;
    }
}
