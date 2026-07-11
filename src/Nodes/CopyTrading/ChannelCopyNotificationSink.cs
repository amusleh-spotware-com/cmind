using System.Threading.Channels;
using Core.Constants;
using Core.CopyTrading;

namespace Nodes.CopyTrading;

// Channel-backed ICopyNotificationSink: the copy host writes operational notifications without blocking;
// the CopyNotificationDrainer (single consumer) resolves the owner and persists them. Bounded + DropOldest
// so a slow DB never back-pressures trading. Notifications are low-volume (breach events only).
public sealed class ChannelCopyNotificationSink : ICopyNotificationSink
{
    private readonly Channel<CopyNotificationRecord> _channel =
        Channel.CreateBounded<CopyNotificationRecord>(new BoundedChannelOptions(CopyDefaults.CopyNotificationChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            // Many profiles per node share this singleton sink → multi-producer; only the drainer reads.
            SingleWriter = false
        });

    public void Notify(CopyNotificationRecord record) => _channel.Writer.TryWrite(record);

    public bool TryRead(out CopyNotificationRecord record) => _channel.Reader.TryRead(out record!);
}
