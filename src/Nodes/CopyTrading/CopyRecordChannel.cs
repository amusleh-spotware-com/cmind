using System.Threading.Channels;

namespace Nodes.CopyTrading;

// Shared bounded, multi-producer / single-consumer channel behind the copy host's out-of-band sinks
// (execution facts, operational notifications). DropOldest so a slow DB never back-pressures trading — a
// dropped telemetry/notification row is always preferable to delaying a copy. The host writes; the matching
// CopyChannelDrainer<T> is the single reader.
public abstract class CopyRecordChannel<T>
{
    private readonly Channel<T> _channel;

    protected CopyRecordChannel(int capacity) =>
        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    protected void Write(T record) => _channel.Writer.TryWrite(record);

    public bool TryRead(out T record) => _channel.Reader.TryRead(out record!);
}
