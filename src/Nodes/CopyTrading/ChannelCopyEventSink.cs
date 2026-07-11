using Core.Constants;
using Core.CopyTrading;

namespace Nodes.CopyTrading;

// Channel-backed ICopyEventSink: the copy host writes execution facts without blocking; the
// CopyExecutionDrainer is the single consumer. Behaviour (bounded, DropOldest, MPSC) is in CopyRecordChannel.
public sealed class ChannelCopyEventSink()
    : CopyRecordChannel<CopyExecutionRecord>(CopyDefaults.CopyExecutionChannelCapacity), ICopyEventSink
{
    public void Record(CopyExecutionRecord record) => Write(record);
}
