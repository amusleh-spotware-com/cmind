using Core;
using Core.Domain;
using Core.Logging;
using Core.PropFirm;
using CTraderOpenApi.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Nodes.PropFirm;

/// <summary>The resolved live-tracking plan for one challenge: its account, credentials and current token.</summary>
public sealed record PropFirmTrackingPlan(
    PropFirmChallengeId ChallengeId,
    UserId UserId,
    bool IsLive,
    string ClientId,
    string ClientSecret,
    long CtidTraderAccountId,
    string AccessToken,
    long TokenVersion);

/// <summary>
/// A read-only tracker for a single prop-firm challenge. It opens a cTrader Open API session for the
/// challenge's account and, on a poll interval, computes live equity (<c>balance + Σ unrealized P&amp;L</c>
/// via <see cref="PropFirmEquityCalculator"/>) and feeds it to the aggregate through <c>RecordEquity</c>.
/// The aggregate owns every rule decision; the host only produces snapshots and persists. When the
/// challenge leaves <see cref="ChallengeStatus.Active"/> (passed/failed/stopped) the tracker exits.
/// </summary>
public sealed class PropFirmTrackingHost(
    PropFirmTrackingPlan plan,
    IOpenApiTradingSessionFactory sessionFactory,
    PropFirmEquityCalculator calculator,
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    TimeSpan pollInterval,
    double drawdownWarnThreshold,
    ILogger<PropFirmTrackingHost> log)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IOpenApiTradingSession? _session;
    private volatile string _accessToken = plan.AccessToken;

    public async Task RunAsync(CancellationToken ct)
    {
        _session = sessionFactory.Create(plan.IsLive, plan.ClientId, plan.ClientSecret);
        _session.AttachAccount(plan.CtidTraderAccountId, _accessToken);
        await _session.StartAsync(ct);
        log.PropFirmTrackerStarted(plan.ChallengeId.Value, plan.CtidTraderAccountId);

        using var timer = new PeriodicTimer(pollInterval);
        do
        {
            try
            {
                if (await PollOnceAsync(ct)) break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                log.PropFirmTrackerFailed(plan.ChallengeId.Value, ex);
            }
        } while (await WaitAsync(timer, ct));

        await DisposeSessionAsync();
    }

    /// <summary>Swaps the account access token in place after a rotation, without dropping the session.</summary>
    public async Task PushTokenUpdateAsync(string token, CancellationToken ct)
    {
        if (_session is null)
        {
            // Not started yet — the next RunAsync attaches with the latest token.
            _accessToken = token;
            return;
        }

        // Swap on the live socket first; only adopt the new token in memory once the swap succeeds, so a
        // failed rotation does not leave the host's token out of sync with the session.
        await _session.SwapAccessTokenAsync(plan.CtidTraderAccountId, token, ct);
        _accessToken = token;
        log.PropFirmTrackerTokenSwapped(plan.ChallengeId.Value, plan.CtidTraderAccountId);
    }

    internal async Task<bool> PollOnceAsync(CancellationToken ct)
    {
        var session = _session ?? throw new InvalidOperationException("Session not started.");
        var balance = await session.LoadBalanceAsync(plan.CtidTraderAccountId, ct);
        var positions = await session.LoadPositionValuationsAsync(plan.CtidTraderAccountId, ct);

        var pricing = new Dictionary<long, SymbolPricing>();
        foreach (var symbolId in positions.Select(p => p.SymbolId).Distinct())
        {
            var (bid, ask) = await session.LoadSpotPriceAsync(plan.CtidTraderAccountId, symbolId, ct);
            pricing[symbolId] = new SymbolPricing(symbolId, bid, ask);
        }

        var result = calculator.Compute(balance, positions, pricing);
        var now = clock.GetUtcNow();
        var holdingOverWeekend = positions.Count > 0 && now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var activity = new ActivitySnapshot(positions.Count, OpenedInNewsWindow: false, holdingOverWeekend);

        return await PersistAsync(result, activity, now, ct);
    }

    private async Task<bool> PersistAsync(EquityResult result, ActivitySnapshot activity, DateTimeOffset now,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPropFirmChallengeRepository>();
            var challenge = await repo.GetByIdAsync(plan.ChallengeId, plan.UserId, ct);
            if (challenge is null || challenge.Status != ChallengeStatus.Active) return true;

            challenge.SetDrawdownWarnThreshold(drawdownWarnThreshold);
            var equity = new Money(Math.Max(0m, (decimal)result.Equity));
            var accountBalance = new Money(Math.Max(0m, (decimal)result.Balance));
            challenge.RecordEquity(new EquitySnapshot(equity, accountBalance), now);
            if (challenge.Status == ChallengeStatus.Active)
                challenge.RecordActivity(activity, now);

            await repo.SaveChangesAsync(ct);
            log.PropFirmEquityRecorded(challenge.Id.Value, challenge.CurrentEquity, challenge.Status.ToString());

            if (challenge.Status == ChallengeStatus.Active) return false;

            log.PropFirmChallengeResolved(challenge.Id.Value, challenge.Status.ToString(), challenge.Breach.ToString());
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task DisposeSessionAsync()
    {
        if (_session is null) return;
        try
        {
            await _session.DisposeAsync();
        }
        catch (Exception ex)
        {
            log.PropFirmTrackerFailed(plan.ChallengeId.Value, ex);
        }
    }
}
