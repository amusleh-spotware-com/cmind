using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Core;
using Core.CopyTrading;

namespace Nodes.CopyTrading;

/// <summary>
/// In-memory live-log broker for running copy profiles. The <see cref="CopyEngineHost"/> appends activity
/// lines (write side, <see cref="ICopyLogSink"/>); the Web log hub tails them (read side,
/// <see cref="ICopyLogFeed"/>). Per profile it keeps a bounded ring of the most recent lines (so a viewer
/// that opens mid-run immediately sees recent history) plus a set of live subscriber channels. In-process
/// only: in the default deployment the supervisor hosts copy engines in the same process as the hub, exactly
/// like a cBot run's container log tail is served from the node that owns it.
/// </summary>
public sealed class CopyLogBroker : ICopyLogSink, ICopyLogFeed
{
    private const int RingCapacity = 500;

    private sealed class ProfileLog
    {
        public readonly Lock Gate = new();
        public readonly Queue<string> Ring = new();
        public readonly HashSet<Channel<string>> Subscribers = [];
    }

    private readonly ConcurrentDictionary<CopyProfileId, ProfileLog> _logs = new();

    public void Append(CopyProfileId profileId, string line)
    {
        var log = _logs.GetOrAdd(profileId, static _ => new ProfileLog());
        lock (log.Gate)
        {
            log.Ring.Enqueue(line);
            while (log.Ring.Count > RingCapacity) log.Ring.Dequeue();
            foreach (var subscriber in log.Subscribers) subscriber.Writer.TryWrite(line);
        }
    }

    public void Complete(CopyProfileId profileId)
    {
        if (!_logs.TryRemove(profileId, out var log)) return;
        lock (log.Gate)
            foreach (var subscriber in log.Subscribers)
                subscriber.Writer.TryComplete();
    }

    public async IAsyncEnumerable<string> TailAsync(
        CopyProfileId profileId, [EnumeratorCancellation] CancellationToken ct)
    {
        var log = _logs.GetOrAdd(profileId, static _ => new ProfileLog());
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

        string[] history;
        // Snapshot the ring and register the live subscriber under the same lock so a line appended
        // concurrently lands in exactly one of the two (history OR channel) — never lost, never duplicated.
        lock (log.Gate)
        {
            history = [.. log.Ring];
            log.Subscribers.Add(channel);
        }

        try
        {
            foreach (var line in history) yield return line;
            await foreach (var line in channel.Reader.ReadAllAsync(ct)) yield return line;
        }
        finally
        {
            lock (log.Gate) log.Subscribers.Remove(channel);
            channel.Writer.TryComplete();
        }
    }
}
