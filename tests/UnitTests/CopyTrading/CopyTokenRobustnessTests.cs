using Core;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

// M1 (Phase 0.5): token invalidation is the #1 real-world copy failure. When a destination's access token
// is invalidated (a partial/again-auth on the cID kills the old token), the host must raise a distinct
// CopyTokenInvalidated alert (EventId 1078) rather than a generic failure, and auto-recover in place when
// the supervisor pushes the refreshed token — no manual re-add. Driven against the faithful fake, which
// now throws the real OpenApiException(TokenInvalid) exactly as the live server does.
public sealed class CopyTokenRobustnessTests
{
    private const long Source = 100;
    private const long Slave = 200;
    private const long SymbolId = 1;
    private const int CopyTokenInvalidatedEventId = 1078;
    private static readonly SymbolDetails Details = new(SymbolId, LotSize: 100, StepVolume: 1, MinVolume: 1, PipPosition: 5);

    private static FakeTradingSession NewSession()
        => new(
            new Dictionary<long, string> { [SymbolId] = "EURUSD" },
            new Dictionary<string, long> { ["EURUSD"] = SymbolId },
            Details);

    private static CopyDestination Destination()
    {
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        return profile.AddDestination(TradingAccountId.New(), RiskSettings.Default);
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(3);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < limit)
        {
            if (condition()) return;
            await Task.Delay(20);
        }
        condition().Should().BeTrue("the expected host action did not occur in time");
    }

    [Fact]
    public async Task Destination_token_invalidation_alerts_then_auto_recovers_on_refresh()
    {
        var session = NewSession();
        var log = new CapturingLogger();
        session.AttachAccount(Slave, "stale"); // match the plan's token, then kill it
        session.InvalidateToken(Slave); // the slave's live token is dead
        var plan = new CopyProfilePlan(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1,
            [new CopyDestinationPlan(Slave, "stale", 1, Destination())]);

        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System, log);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try
        {
            // A master open while the slave token is invalid -> the copy fails with a token-invalid alert.
            session.PushOpen(Source, positionId: 1001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => log.Records.Any(r => r.EventId == CopyTokenInvalidatedEventId));
            session.Orders.Should().BeEmpty("no copy is placed on an account with a dead token");

            // The supervisor pushes a refreshed token -> in-place swap clears the invalid token (recovery).
            host.PushTokenUpdate([(Slave, "fresh")]);
            await WaitUntil(() => session.Swaps.Any(s => s.Ctid == Slave && s.Token == "fresh"));

            // A subsequent master open now copies normally — copying resumed with no manual re-add.
            session.PushOpen(Source, positionId: 1002, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Any(o => o.Label == "1002"));
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }

        log.Records.Should().Contain(r => r.EventId == CopyTokenInvalidatedEventId,
            "an invalidated token raises the CopyTokenInvalidated alert");
        session.Orders.Should().ContainSingle(o => o.Label == "1002", "copying auto-recovers after the token refresh");
    }
}
