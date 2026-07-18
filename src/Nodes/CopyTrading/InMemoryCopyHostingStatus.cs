using System.Collections.Concurrent;
using Core;
using Core.CopyTrading;

namespace Nodes.CopyTrading;

/// <summary>
/// In-process, node-local implementation of <see cref="ICopyHostingStatus"/>: a thread-safe map of the
/// copy profiles this node is hosting to their current warming/ready phase. Registered as a singleton and
/// shared between the copy supervisor/host (writers) and the Web copy endpoints (readers) in the same
/// process, so the UI can show "Starting" until the host has finished its first resync.
/// </summary>
public sealed class InMemoryCopyHostingStatus : ICopyHostingStatus
{
    private readonly ConcurrentDictionary<CopyProfileId, CopyHostingPhase> _phases = new();

    public void MarkWarming(CopyProfileId profileId) => _phases[profileId] = CopyHostingPhase.Warming;

    public void MarkReady(CopyProfileId profileId) => _phases[profileId] = CopyHostingPhase.Ready;

    public void Clear(CopyProfileId profileId) => _phases.TryRemove(profileId, out _);

    public CopyHostingPhase PhaseOf(CopyProfileId profileId)
        => _phases.TryGetValue(profileId, out var phase) ? phase : CopyHostingPhase.NotHosted;
}
