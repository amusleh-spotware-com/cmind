using System.Collections.Concurrent;
using Core;
using Core.Ai;

namespace Infrastructure.Ai;

/// <summary>Thread-safe in-memory implementation of <see cref="IAiBuildActivity"/> (registered as a singleton).</summary>
public sealed class AiBuildActivity : IAiBuildActivity
{
    private readonly ConcurrentDictionary<CBotSourceProjectId, byte> _working = new();

    public void MarkWorking(CBotSourceProjectId projectId) => _working[projectId] = 0;

    public void Clear(CBotSourceProjectId projectId) => _working.TryRemove(projectId, out _);

    public bool IsWorking(CBotSourceProjectId projectId) => _working.ContainsKey(projectId);

    public IReadOnlyCollection<Guid> WorkingProjectIds() => _working.Keys.Select(k => k.Value).ToArray();
}
