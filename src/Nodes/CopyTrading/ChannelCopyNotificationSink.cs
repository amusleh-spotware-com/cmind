using Core.Constants;
using Core.CopyTrading;

namespace Nodes.CopyTrading;

// Channel-backed ICopyNotificationSink: the copy host writes operational notifications without blocking; the
// CopyNotificationDrainer is the single consumer. Behaviour is in CopyRecordChannel (bounded, DropOldest).
public sealed class ChannelCopyNotificationSink()
    : CopyRecordChannel<CopyNotificationRecord>(CopyDefaults.CopyNotificationChannelCapacity), ICopyNotificationSink
{
    public void Notify(CopyNotificationRecord record) => Write(record);
}
