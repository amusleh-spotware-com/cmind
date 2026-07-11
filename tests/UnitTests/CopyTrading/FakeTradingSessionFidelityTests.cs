using CTraderOpenApi.Client;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

// Simulator-fidelity tests: assert FakeTradingSession reproduces the real cTrader Open API server
// behaviors the plan's Phase 0a calls out (F1 partial fill, F2 volume normalization, F5 market-range by
// spot, F12 typed rejections, F13 token invalidation). These lock the "extend, never weaken" contract:
// the fake is only trustworthy for the later phases if these hold.
public sealed class FakeTradingSessionFidelityTests
{
    private const long Ctid = 200;
    private const long SymbolId = 1;
    private const string Token = "tok-1";

    private static FakeTradingSession NewSession(int pipPosition = 5)
    {
        var session = new FakeTradingSession(
            new Dictionary<long, string> { [SymbolId] = "EURUSD" },
            new Dictionary<string, long> { ["EURUSD"] = SymbolId },
            new SymbolDetails(SymbolId, LotSize: 100, StepVolume: 1, MinVolume: 1, PipPosition: pipPosition));
        session.AttachAccount(Ctid, Token);
        return session;
    }

    private static long FilledVolume(FakeTradingSession session)
        => session.ReconcileAsync(Ctid, CancellationToken.None).GetAwaiter().GetResult().Single().Volume;

    [Fact]
    public async Task F2_volume_is_rounded_down_to_step()
    {
        var session = NewSession();
        session.VolumeBoundsForCtid[Ctid] = (Step: 10, Min: 10, Max: 1000);

        await session.SendMarketOrderAsync(Ctid, SymbolId, isBuy: true, volume: 37, "l", CancellationToken.None);

        FilledVolume(session).Should().Be(30, "37 rounds down to the nearest step of 10");
    }

    [Fact]
    public async Task F2_below_min_volume_is_rejected()
    {
        var session = NewSession();
        session.VolumeBoundsForCtid[Ctid] = (Step: 10, Min: 100, Max: 1000);

        var act = () => session.SendMarketOrderAsync(Ctid, SymbolId, isBuy: true, volume: 50, "l", CancellationToken.None);

        (await act.Should().ThrowAsync<CtraderRejectException>()).Which.Reason
            .Should().Be(CtraderRejectReason.VolumeTooLow);
    }

    [Fact]
    public async Task F2_above_max_volume_is_rejected()
    {
        var session = NewSession();
        session.VolumeBoundsForCtid[Ctid] = (Step: 10, Min: 10, Max: 100);

        var act = () => session.SendMarketOrderAsync(Ctid, SymbolId, isBuy: true, volume: 500, "l", CancellationToken.None);

        (await act.Should().ThrowAsync<CtraderRejectException>()).Which.Reason
            .Should().Be(CtraderRejectReason.VolumeTooHigh);
    }

    [Fact]
    public async Task F1_partial_fill_reduces_the_filled_volume()
    {
        var session = NewSession();
        session.PartialFillFractionForCtid[Ctid] = 0.6;

        await session.SendMarketOrderAsync(Ctid, SymbolId, isBuy: true, volume: 100, "l", CancellationToken.None);

        FilledVolume(session).Should().Be(60, "only 60% of the requested volume fills");
        session.Orders.Single().Volume.Should().Be(100, "the requested order volume is still recorded");
    }

    [Fact]
    public async Task F12_typed_rejection_is_one_shot()
    {
        var session = NewSession();
        session.RejectReasonForCtid[Ctid] = CtraderRejectReason.NotEnoughMoney;

        var first = () => session.SendMarketOrderAsync(Ctid, SymbolId, isBuy: true, volume: 100, "l", CancellationToken.None);
        (await first.Should().ThrowAsync<CtraderRejectException>()).Which.Reason
            .Should().Be(CtraderRejectReason.NotEnoughMoney);

        await session.SendMarketOrderAsync(Ctid, SymbolId, isBuy: true, volume: 100, "l", CancellationToken.None);
        session.Orders.Should().ContainSingle("the reject is consumed once, the retry succeeds");
    }

    [Fact]
    public async Task F12_close_can_reject_position_not_found()
    {
        var session = NewSession();
        session.RejectReasonForCtid[Ctid] = CtraderRejectReason.PositionNotFound;

        var act = () => session.ClosePositionAsync(Ctid, positionId: 5001, volume: 100, CancellationToken.None);

        (await act.Should().ThrowAsync<CtraderRejectException>()).Which.Reason
            .Should().Be(CtraderRejectReason.PositionNotFound);
    }

    [Fact]
    public async Task F13_invalidated_token_rejects_orders_until_swapped()
    {
        var session = NewSession();
        session.InvalidateToken(Ctid);

        var act = () => session.SendMarketOrderAsync(Ctid, SymbolId, isBuy: true, volume: 100, "l", CancellationToken.None);
        await act.Should().ThrowAsync<CtraderTokenInvalidException>();

        await session.SwapAccessTokenAsync(Ctid, "tok-2", CancellationToken.None);
        await session.SendMarketOrderAsync(Ctid, SymbolId, isBuy: true, volume: 100, "l", CancellationToken.None);
        session.Orders.Should().ContainSingle("a fresh token restores trading");
    }

    [Fact]
    public async Task F13_invalidated_token_also_rejects_close_amend_and_cancel()
    {
        var session = NewSession();
        session.InvalidateToken(Ctid);

        var close = () => session.ClosePositionAsync(Ctid, 5001, 100, CancellationToken.None);
        var amendSltp = () => session.AmendPositionSltpAsync(Ctid, 5001, 1.09, null, false, CancellationToken.None);
        var cancel = () => session.CancelOrderAsync(Ctid, 8001, CancellationToken.None);
        var amendPending = () => session.AmendPendingOrderAsync(Ctid, 8001, CopyOrderKind.Limit, 100, 1.05, null, null, CancellationToken.None);

        await close.Should().ThrowAsync<CtraderTokenInvalidException>();
        await amendSltp.Should().ThrowAsync<CtraderTokenInvalidException>();
        await cancel.Should().ThrowAsync<CtraderTokenInvalidException>();
        await amendPending.Should().ThrowAsync<CtraderTokenInvalidException>();
    }

    [Fact]
    public async Task F5_market_range_fills_within_tolerance_and_rejects_beyond()
    {
        var withinSession = NewSession();
        withinSession.SetSpot(SymbolId, bid: 1.09995, ask: 1.10005);
        await withinSession.SendMarketOrderAsync(Ctid, SymbolId, isBuy: true, volume: 100, "l",
            CancellationToken.None, slippageInPoints: 10, baseSlippagePrice: 1.10);
        withinSession.Orders.Should().ContainSingle("spot within base ± 10 points fills");

        var beyondSession = NewSession();
        beyondSession.SetSpot(SymbolId, bid: 1.1001, ask: 1.1002);
        var act = () => beyondSession.SendMarketOrderAsync(Ctid, SymbolId, isBuy: true, volume: 100, "l",
            CancellationToken.None, slippageInPoints: 10, baseSlippagePrice: 1.10);
        (await act.Should().ThrowAsync<CtraderRejectException>()).Which.Reason
            .Should().Be(CtraderRejectReason.MarketRangeExceeded);
    }

    [Fact]
    public async Task Legacy_market_range_flag_still_forces_a_reject()
    {
        var session = NewSession();
        session.RejectMarketRangeForCtid.Add(Ctid);

        var act = () => session.SendMarketOrderAsync(Ctid, SymbolId, isBuy: true, volume: 100, "l",
            CancellationToken.None, slippageInPoints: 10, baseSlippagePrice: 1.10);

        await act.Should().ThrowAsync<CtraderRejectException>();
    }
}
