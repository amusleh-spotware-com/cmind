using Core;
using FluentAssertions;
using Nodes.CopyTrading;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class CopyLogBrokerTests
{
    private static async Task WaitFor(Func<bool> condition, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(5);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < limit)
        {
            if (condition()) return;
            await Task.Delay(15);
        }

        condition().Should().BeTrue("the expected broker lines did not arrive in time");
    }

    [Fact]
    public async Task Tail_replays_buffered_history_then_streams_live_lines()
    {
        var broker = new CopyLogBroker();
        var profile = CopyProfileId.New();
        broker.Append(profile, "line-1");
        broker.Append(profile, "line-2");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<string>();
        var tail = Task.Run(async () =>
        {
            try
            {
                await foreach (var line in broker.TailAsync(profile, cts.Token))
                    lock (received) received.Add(line);
            }
            catch (OperationCanceledException) { /* expected on cancel */ }
        });

        await WaitFor(() => Count(received) >= 2);
        broker.Append(profile, "line-3");
        broker.Append(profile, "line-4");
        await WaitFor(() => Count(received) >= 4);
        await cts.CancelAsync();
        try { await tail; } catch { /* cancellation */ }

        lock (received) received.Should().Equal("line-1", "line-2", "line-3", "line-4");
    }

    [Fact]
    public async Task Tail_is_isolated_per_profile()
    {
        var broker = new CopyLogBroker();
        var watched = CopyProfileId.New();
        var other = CopyProfileId.New();
        broker.Append(other, "other-history");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var received = new List<string>();
        var tail = Task.Run(async () =>
        {
            try
            {
                await foreach (var line in broker.TailAsync(watched, cts.Token))
                    lock (received) received.Add(line);
            }
            catch (OperationCanceledException) { /* expected on cancel */ }
        });

        await Task.Delay(150);
        broker.Append(other, "other-live");
        broker.Append(watched, "watched-live");
        await WaitFor(() => Count(received) >= 1);
        await cts.CancelAsync();
        try { await tail; } catch { /* cancellation */ }

        lock (received) received.Should().ContainSingle().Which.Should().Be("watched-live");
    }

    [Fact]
    public async Task Tail_history_is_bounded_to_the_most_recent_lines()
    {
        var broker = new CopyLogBroker();
        var profile = CopyProfileId.New();
        for (var i = 0; i < 700; i++) broker.Append(profile, $"line-{i}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var history = new List<string>();
        var tail = Task.Run(async () =>
        {
            try
            {
                await foreach (var line in broker.TailAsync(profile, cts.Token))
                    lock (history) history.Add(line);
            }
            catch (OperationCanceledException) { /* expected on cancel */ }
        });

        // History is replayed up front; give it a beat, then stop and assert only the last 500 survive.
        await Task.Delay(300);
        await cts.CancelAsync();
        try { await tail; } catch { /* cancellation */ }

        lock (history)
        {
            history.Should().HaveCount(500);
            history[0].Should().Be("line-200");
            history[^1].Should().Be("line-699");
        }
    }

    [Fact]
    public async Task Complete_ends_open_tails_and_drops_buffered_history()
    {
        var broker = new CopyLogBroker();
        var profile = CopyProfileId.New();
        broker.Append(profile, "before");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<string>();
        var tail = Task.Run(async () =>
        {
            await foreach (var line in broker.TailAsync(profile, cts.Token))
                lock (received) received.Add(line);
        });

        await WaitFor(() => Count(received) >= 1);
        broker.Complete(profile);
        await tail; // completes because the channel was completed — not because the token cancelled

        lock (received) received.Should().ContainSingle().Which.Should().Be("before");

        // A fresh tail after Complete sees no buffered history (the ring was dropped, no leak).
        using var after = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        var replay = new List<string>();
        try
        {
            await foreach (var line in broker.TailAsync(profile, after.Token)) replay.Add(line);
        }
        catch (OperationCanceledException) { /* expected — nothing to replay, so it blocks then cancels */ }

        replay.Should().BeEmpty("Complete dropped the profile's buffered history");
    }

    private static int Count(List<string> list) { lock (list) return list.Count; }
}
