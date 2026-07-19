namespace Core.Ai;

/// <summary>
/// Node-local, in-memory record of which AI Build projects are currently generating a model reply. A prompt
/// runs detached from the HTTP request (so it keeps going when the user navigates away); this tracks the
/// in-flight work so the UI can show a "Working" status per project. Runtime only — not persisted; a host
/// restart drops the (already-dead) work and clears the flag.
/// </summary>
public interface IAiBuildActivity
{
    void MarkWorking(Core.CBotSourceProjectId projectId);
    void Clear(Core.CBotSourceProjectId projectId);
    bool IsWorking(Core.CBotSourceProjectId projectId);
    IReadOnlyCollection<Guid> WorkingProjectIds();
}
