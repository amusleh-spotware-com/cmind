using System.Text;
using Core;
using Core.Agent;
using Core.Constants;
using CTraderOpenApi.Client;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Agent;

/// <summary>Resolved Open API credentials for one managed account, or null when it is not linked/authorized.</summary>
internal sealed record AccountCredentials(bool Live, string ClientId, string ClientSecret, string AccessToken, long Ctid);

/// <summary>Resolves a managed account's live Open API credentials (mirrors the copy-equity reader).</summary>
internal static class AgentAccountResolver
{
    public static async Task<AccountCredentials?> ResolveAsync(
        DataContext db, ISecretProtector protector, TradingAccountId accountId, CancellationToken ct)
    {
        var account = await db.TradingAccounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account?.OpenApiAuthorizationId is null || account.CtidTraderAccountId is not { } ctid) return null;

        var auth = await db.OpenApiAuthorizations.FirstOrDefaultAsync(a => a.Id == account.OpenApiAuthorizationId, ct);
        if (auth is null) return null;
        var application = await db.OpenApiApplications.FirstOrDefaultAsync(a => a.Id == auth.ApplicationId, ct);
        if (application is null) return null;

        var clientSecret = Encoding.UTF8.GetString(
            protector.Unprotect(application.EncryptedClientSecret, EncryptionPurposes.OpenApiClientSecret));
        var token = Encoding.UTF8.GetString(
            protector.Unprotect(auth.EncryptedAccessToken, EncryptionPurposes.OpenApiAccessToken));
        if (string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(token)) return null;

        return new AccountCredentials(account.IsLive, application.ClientId, clientSecret, token, ctid);
    }
}

/// <summary>
/// Live account-state store over the cTrader Open API: reads balance and open positions and computes the
/// open exposure in lots (the envelope-critical number). Degrades to a safe flat state when the account
/// is not linked or the connection fails, so the runtime and safety gate never break.
/// </summary>
public sealed class OpenApiAccountStateStore(
    IServiceScopeFactory scopes, IOpenApiTradingSessionFactory sessions) : IAccountStateStore
{
    public async Task<AccountState> GetStateAsync(TradingAccountId account, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

            var creds = await AgentAccountResolver.ResolveAsync(db, protector, account, ct);
            if (creds is null) return Flat(account);

            await using var session = sessions.Create(creds.Live, creds.ClientId, creds.ClientSecret);
            session.AttachAccount(creds.Ctid, creds.AccessToken);
            await session.StartAsync(ct);

            var balance = await session.LoadBalanceAsync(creds.Ctid, ct);
            var positions = await session.ReconcileAsync(creds.Ctid, ct);
            var exposureLots = await OpenExposureLotsAsync(session, creds.Ctid, positions, ct);

            return new AccountState(account, (decimal)balance, (decimal)balance, exposureLots,
                ConsecutiveLosses: 0, DailyLossPercent: 0, OrdersThisHour: 0);
        }
        catch
        {
            return Flat(account); // never throw into the runtime — a blind read holds
        }
    }

    private static async Task<double> OpenExposureLotsAsync(
        IOpenApiTradingSession session, long ctid, IReadOnlyList<OpenPositionSnapshot> positions, CancellationToken ct)
    {
        if (positions.Count == 0) return 0;
        var symbolIds = positions.Select(p => p.SymbolId).Distinct().ToArray();
        var details = await session.LoadSymbolDetailsAsync(ctid, symbolIds, ct);
        var lotSize = details.ToDictionary(d => d.SymbolId, d => d.LotSize);

        var total = 0.0;
        foreach (var p in positions)
            if (lotSize.TryGetValue(p.SymbolId, out var size) && size > 0)
                total += (double)p.Volume / size;
        return total;
    }

    private static AccountState Flat(TradingAccountId account) =>
        new(account, 0m, 0m, 0, 0, 0, 0);
}

/// <summary>
/// Live order executor over the cTrader Open API: places a market order for a cleared agent decision,
/// converting lots to wire volume via the symbol's lot size. Degrades to "not executed" on any failure
/// so a cleared order is never silently assumed filled. Only ever reached for a Full-Auto agent whose
/// order passed the risk envelope, with the runtime explicitly enabled.
/// </summary>
public sealed class OpenApiAgentOrderExecutor(
    IServiceScopeFactory scopes, IOpenApiTradingSessionFactory sessions) : IAgentOrderExecutor
{
    public async Task<bool> ExecuteAsync(TradingAgent agent, AgentOrderIntent order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(order);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

            var creds = await AgentAccountResolver.ResolveAsync(db, protector, order.Account, ct);
            if (creds is null) return false;

            await using var session = sessions.Create(creds.Live, creds.ClientId, creds.ClientSecret);
            session.AttachAccount(creds.Ctid, creds.AccessToken);
            await session.StartAsync(ct);

            var symbolIds = await session.LoadSymbolIdsAsync(creds.Ctid, ct);
            var key = new string(order.Symbol.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (!symbolIds.TryGetValue(key, out var symbolId)) return false;

            var details = await session.LoadSymbolDetailsAsync(creds.Ctid, [symbolId], ct);
            var lotSize = details.Count > 0 ? details[0].LotSize : 0;
            if (lotSize <= 0) return false;

            var volume = (long)Math.Round(order.SizeLots * lotSize, MidpointRounding.AwayFromZero);
            var minVolume = details[0].MinVolume;
            if (volume < minVolume) volume = minVolume;
            if (volume <= 0) return false;

            var isBuy = order.Side == Core.Execution.OrderSide.Buy;
            await session.SendMarketOrderAsync(creds.Ctid, symbolId, isBuy, volume, $"agent:{agent.Id.Value:N}", ct);
            return true;
        }
        catch
        {
            return false; // never throw into the runtime; a failed send is recorded as not-executed
        }
    }
}
