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
}
