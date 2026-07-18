using CTraderOpenApi;
using CTraderOpenApi.Client;
using CTraderOpenApi.Messages;
using FluentAssertions;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.CopyTrading;

// Tests the WIRE seam FakeTradingSession bypasses: how a raw ProtoOAExecutionEvent is classified into an
// ExecutionEvent. The take-profit bug and (suspected) pending-order bug both live in seams like this that
// the simulator can't reach — so pending placement/cancel must be asserted straight off the protocol.
public sealed class OpenApiTradingSessionWireTests
{
    private const long Account = 777;

    private static OpenApiConnectionOptions FastOptions => new()
    {
        HeartbeatInterval = TimeSpan.FromSeconds(30),
        RequestTimeout = TimeSpan.FromSeconds(2),
        InboundWatchdogTimeout = TimeSpan.FromSeconds(30),
        BackoffInitial = TimeSpan.FromMilliseconds(10),
        BackoffMax = TimeSpan.FromMilliseconds(50)
    };

    private static async Task<(OpenApiTradingSession Session, FakeOpenApiTransport Transport)> ConnectedAsync()
    {
        var factory = new FakeOpenApiTransportFactory(FakeResponders.Handshake);
        var connection = new OpenApiConnection(factory, "host", 5035, "client", "secret", FastOptions,
            NullLogger<OpenApiConnection>.Instance, TimeProvider.System);
        var session = new OpenApiTradingSession(connection);
        session.AttachAccount(Account, "token");
        await session.StartAsync(CancellationToken.None);
        return (session, factory.Created[0]);
    }

    private static ProtoMessage ExecutionMessage(ProtoOAExecutionEvent executionEvent) => new()
    {
        PayloadType = (uint)ProtoOAPayloadType.ProtoOaExecutionEvent,
        Payload = executionEvent.ToByteString()
    };

    private static async Task<ExecutionEvent> FirstExecutionAsync(OpenApiTradingSession session)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var e in session.SourceExecutionsAsync(Account, cts.Token))
            return e;
        throw new InvalidOperationException("no execution event was yielded");
    }

    [Theory]
    [InlineData(ProtoOAOrderType.Limit, CopyOrderKind.Limit)]
    [InlineData(ProtoOAOrderType.Stop, CopyOrderKind.Stop)]
    public async Task Pending_order_placement_is_classified_as_a_pending_execution_event(
        ProtoOAOrderType wireType, CopyOrderKind expectedKind)
    {
        var (session, transport) = await ConnectedAsync();
        await using var _ = session;

        var order = new ProtoOAOrder
        {
            OrderId = 5001,
            OrderType = wireType,
            TradeData = new ProtoOATradeData { SymbolId = 42, TradeSide = ProtoOATradeSide.Buy, Volume = 100, Label = "src" }
        };
        if (wireType == ProtoOAOrderType.Limit) order.LimitPrice = 1.2345;
        else order.StopPrice = 1.2345;

        transport.Push(ExecutionMessage(new ProtoOAExecutionEvent
        {
            CtidTraderAccountId = Account,
            ExecutionType = ProtoOAExecutionType.OrderAccepted,
            Order = order
        }));

        var execution = await FirstExecutionAsync(session);

        execution.IsPendingOrder.Should().BeTrue("a limit/stop order placement must be a pending event, not a position");
        execution.IsOrderCancelled.Should().BeFalse();
        execution.OrderKind.Should().Be(expectedKind);
        execution.OrderId.Should().Be(5001);
        execution.SymbolId.Should().Be(42);
        execution.IsBuy.Should().BeTrue();
        execution.Volume.Should().Be(100);
    }

    [Fact]
    public async Task Pending_order_relative_sltp_and_trailing_are_classified_onto_the_execution_event()
    {
        // A resting pending carries its protection as RELATIVE distances + a trailing flag on the ProtoOAOrder
        // (the absolute StopLoss/TakeProfit stay unset until it fills). This is the seam where "a pending with
        // both SL and TP copied neither" and "trailing never copies" lived — assert the raw parse.
        var (session, transport) = await ConnectedAsync();
        await using var _ = session;

        transport.Push(ExecutionMessage(new ProtoOAExecutionEvent
        {
            CtidTraderAccountId = Account,
            ExecutionType = ProtoOAExecutionType.OrderAccepted,
            Order = new ProtoOAOrder
            {
                OrderId = 5003,
                OrderType = ProtoOAOrderType.Stop,
                StopPrice = 1.2345,
                RelativeStopLoss = 100_000,
                RelativeTakeProfit = 200_000,
                TrailingStopLoss = true,
                TradeData = new ProtoOATradeData { SymbolId = 42, TradeSide = ProtoOATradeSide.Buy, Volume = 100 }
            }
        }));

        var execution = await FirstExecutionAsync(session);

        execution.IsPendingOrder.Should().BeTrue();
        execution.RelativeStopLoss.Should().Be(100_000, "a pending's stop-loss rides RelativeStopLoss, not the absolute field");
        execution.RelativeTakeProfit.Should().Be(200_000);
        execution.TrailingStopLoss.Should().BeTrue("the pending's trailing-stop flag must reach the engine");
        execution.StopLoss.Should().BeNull("the absolute price is unset on a resting pending");
        execution.TakeProfit.Should().BeNull();
    }

    [Fact]
    public async Task Pending_order_cancel_is_classified_as_a_cancelled_pending_event()
    {
        var (session, transport) = await ConnectedAsync();
        await using var _ = session;

        transport.Push(ExecutionMessage(new ProtoOAExecutionEvent
        {
            CtidTraderAccountId = Account,
            ExecutionType = ProtoOAExecutionType.OrderCancelled,
            Order = new ProtoOAOrder
            {
                OrderId = 5002,
                OrderType = ProtoOAOrderType.Limit,
                LimitPrice = 1.2345,
                TradeData = new ProtoOATradeData { SymbolId = 42, TradeSide = ProtoOATradeSide.Sell, Volume = 100 }
            }
        }));

        var execution = await FirstExecutionAsync(session);

        execution.IsPendingOrder.Should().BeTrue();
        execution.IsOrderCancelled.Should().BeTrue("an order-cancelled event for a resting pending must mirror the cancel");
        execution.OrderId.Should().Be(5002);
    }
}
