using System.Threading.Channels;
using Core.Constants;
using Core.CopyTrading;

namespace Nodes.CopyTrading;

// Channel-backed ICopyEventSink: the copy host (producer, on the trading hot path) writes execution facts
// without blocking; the CopyExecutionDrainer (single consumer) reads and persists them. Bounded +
// DropOldest so a slow or stalled DB never backpressures order execution — dropping a transparency row is
// always preferable to delaying a copy. Registered as a singleton only when App:Copy:TransparencyEnabled.
public sealed class ChannelCopyEventSink : ICopyEventSink
{
    private readonly Channel<CopyExecutionRecord> _channel =
        Channel.CreateBounded<CopyExecutionRecord>(new BoundedChannelOptions(CopyDefaults.CopyExecutionChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            // A node hosts many profiles concurrently and each CopyEngineHost shares this singleton sink,
            // so writes are multi-producer; only the single drainer reads.
            SingleWriter = false
        });

    public void Record(CopyExecutionRecord record) => _channel.Writer.TryWrite(record);

    public bool TryRead(out CopyExecutionRecord record) => _channel.Reader.TryRead(out record!);
}
