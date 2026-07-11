using Core;
using Core.Domain;
using Core.PropFirm;
using CTraderOpenApi.Client;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Nodes.PropFirm;
using UnitTests.CopyTrading;
using Xunit;

namespace UnitTests.PropFirm;

public class PropFirmTrackingHostTests
{
    private const long OneLot = 10_000_000;
    private const long SymbolId = 10;

    private sealed class InMemoryChallengeRepository(PropFirmChallenge challenge) : IPropFirmChallengeRepository
    {
        public PropFirmChallenge Challenge { get; } = challenge;

        public Task<PropFirmChallenge?> GetByIdAsync(PropFirmChallengeId id, UserId owner, CancellationToken ct)
            => Task.FromResult(Challenge.Id == id && Challenge.UserId == owner ? Challenge : null);

        public Task<IReadOnlyList<PropFirmChallenge>> ListByUserAsync(UserId owner, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PropFirmChallenge>>([Challenge]);

        public Task AddAsync(PropFirmChallenge challenge, CancellationToken ct) => Task.CompletedTask;
        public void Remove(PropFirmChallenge challenge) { }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static (PropFirmTrackingHost Host, PropFirmChallenge Challenge, FakeTradingSession Session) Build(
        double dailyLoss = 5, double maxDrawdown = 10)
    {
        var challenge = PropFirmChallenge.Create(UserId.New(), TradingAccountId.New(), "Live",
            new Money(100_000m),
            new ChallengeRules(new Percent(10), new Percent(dailyLoss), new Percent(maxDrawdown),
                DrawdownMode.Static, new TradingDayRequirement(0), SingleStep: true));

        var repo = new InMemoryChallengeRepository(challenge);
        var services = new ServiceCollection();
        services.AddScoped<IPropFirmChallengeRepository>(_ => repo);
        var provider = services.BuildServiceProvider();

        var session = new FakeTradingSession(
            new Dictionary<long, string> { [SymbolId] = "EURUSD" },
            new Dictionary<string, long> { ["EURUSD"] = SymbolId },
            new SymbolDetails(SymbolId, 100_000, 100_000, 100_000, 5)) { Balance = 100_000 };
        session.SeedPosition(1, positionId: 500, SymbolId, isBuy: true, OneLot, "live");
        session.SetPositionValuation(positionId: 500, entryPrice: 1.1000);

        var plan = new PropFirmTrackingPlan(challenge.Id, challenge.UserId, IsLive: false, "cid", "secret",
            CtidTraderAccountId: 1, "token", TokenVersion: 1);
        var host = new PropFirmTrackingHost(plan, new FakeTradingSessionFactory(session),
            new PropFirmEquityCalculator(), provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeTimeProvider(new DateTimeOffset(2026, 07, 10, 12, 0, 0, TimeSpan.Zero)),
            TimeSpan.FromMilliseconds(1), drawdownWarnThreshold: 80,
            NullLogger<PropFirmTrackingHost>.Instance);

        return (host, challenge, session);
    }

    [Fact]
    public async Task Tracker_passes_challenge_when_live_equity_reaches_target()
    {
        var (host, challenge, session) = Build();
        // +0.10 on 1 lot = +10_000 -> equity 110_000 -> 10% target hit.
        session.SetSpot(SymbolId, bid: 1.2000, ask: 1.2002);

        await host.RunAsync(CancellationToken.None);

        challenge.Status.Should().Be(ChallengeStatus.Passed);
        challenge.CurrentEquity.Should().BeApproximately(110_000m, 1m);
    }

    [Fact]
    public async Task Tracker_fails_challenge_when_live_equity_breaches_drawdown()
    {
        var (host, challenge, session) = Build(dailyLoss: 50, maxDrawdown: 10);
        // -0.11 on 1 lot = -11_000 -> equity 89_000 -> beyond 10% static drawdown (daily-loss set wide).
        session.SetSpot(SymbolId, bid: 0.9900, ask: 0.9902);

        await host.RunAsync(CancellationToken.None);

        challenge.Status.Should().Be(ChallengeStatus.Failed);
        challenge.Breach.Should().Be(BreachReason.MaxDrawdown);
    }
}
