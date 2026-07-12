using CTraderOpenApi.Messages;
using CTraderOpenApi.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class OpenApiRateLimitTests
{
    [Theory]
    [InlineData((uint)ProtoOAPayloadType.ProtoOaGetTrendbarsReq, OpenApiRateCategory.HistoricalData)]
    [InlineData((uint)ProtoOAPayloadType.ProtoOaGetTickdataReq, OpenApiRateCategory.HistoricalData)]
    [InlineData((uint)ProtoOAPayloadType.ProtoOaApplicationAuthReq, OpenApiRateCategory.Exempt)]
    [InlineData((uint)ProtoOAPayloadType.ProtoOaAccountAuthReq, OpenApiRateCategory.Exempt)]
    [InlineData((uint)ProtoPayloadType.HeartbeatEvent, OpenApiRateCategory.Exempt)]
    [InlineData((uint)ProtoOAPayloadType.ProtoOaNewOrderReq, OpenApiRateCategory.General)]
    public void Classify_maps_payload_to_category(uint payloadType, OpenApiRateCategory expected)
        => OpenApiRateLimits.Classify(payloadType).Should().Be(expected);

    [Fact]
    public async Task Unlimited_bucket_never_delays()
    {
        var time = new FakeTimeProvider();
        var bucket = new TokenBucket(0, time);

        for (var i = 0; i < 50; i++)
        {
            var wait = bucket.WaitAsync(CancellationToken.None);
            wait.IsCompleted.Should().BeTrue();
            await wait;
        }
    }

    [Fact]
    public async Task Bucket_paces_to_configured_rate()
    {
        var time = new FakeTimeProvider();
        var bucket = new TokenBucket(1, time); // 1 msg/sec, capacity 1

        await bucket.WaitAsync(CancellationToken.None); // spend the initial token immediately

        var next = bucket.WaitAsync(CancellationToken.None).AsTask();
        next.IsCompleted.Should().BeFalse();

        time.Advance(TimeSpan.FromSeconds(1));
        await next.WaitAsync(TimeSpan.FromSeconds(5));
        next.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Reset_refills_bucket_so_reconnect_does_not_burst_over_cap()
    {
        var time = new FakeTimeProvider();
        var bucket = new TokenBucket(1, time);

        await bucket.WaitAsync(CancellationToken.None); // drain to 0
        bucket.Reset();                                 // reconnect refills to capacity

        var afterReset = bucket.WaitAsync(CancellationToken.None);
        afterReset.IsCompleted.Should().BeTrue();       // a token is available again, no wall-clock advance
        await afterReset;
    }

    private static OpenApiRateGate Gate(FakeTimeProvider time, int general, int historical) =>
        new(new Dictionary<OpenApiRateCategory, int>
        {
            [OpenApiRateCategory.General] = general,
            [OpenApiRateCategory.HistoricalData] = historical
        }, time);

    [Fact]
    public async Task Gate_never_paces_exempt_messages()
    {
        var time = new FakeTimeProvider();
        var gate = Gate(time, general: 1, historical: 1);

        await gate.AcquireAsync((uint)ProtoOAPayloadType.ProtoOaNewOrderReq, CancellationToken.None); // drain general

        var heartbeat = gate.AcquireAsync((uint)ProtoPayloadType.HeartbeatEvent, CancellationToken.None);
        heartbeat.IsCompleted.Should().BeTrue(); // exempt: sent despite the empty general bucket
        await heartbeat;
    }

    [Fact]
    public async Task Gate_historical_request_also_consumes_the_general_bucket()
    {
        var time = new FakeTimeProvider();
        var gate = Gate(time, general: 1, historical: 1);

        // One historical request draws a token from BOTH the historical and the general bucket.
        await gate.AcquireAsync((uint)ProtoOAPayloadType.ProtoOaGetTrendbarsReq, CancellationToken.None);

        // The general bucket is now empty, so a plain trading message must wait.
        var general = gate.AcquireAsync((uint)ProtoOAPayloadType.ProtoOaNewOrderReq, CancellationToken.None).AsTask();
        general.IsCompleted.Should().BeFalse();

        time.Advance(TimeSpan.FromSeconds(1));
        await general.WaitAsync(TimeSpan.FromSeconds(5));
        general.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Gate_general_traffic_is_not_slowed_by_the_historical_bucket()
    {
        var time = new FakeTimeProvider();
        var gate = Gate(time, general: 5, historical: 1);

        // Deplete the historical bucket (also spends one general token, leaving 4).
        await gate.AcquireAsync((uint)ProtoOAPayloadType.ProtoOaGetTrendbarsReq, CancellationToken.None);

        // General traffic still flows immediately — it is not gated by the drained historical bucket.
        var general = gate.AcquireAsync((uint)ProtoOAPayloadType.ProtoOaNewOrderReq, CancellationToken.None);
        general.IsCompleted.Should().BeTrue();
        await general;
    }
}
