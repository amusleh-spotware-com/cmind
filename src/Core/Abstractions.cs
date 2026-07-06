namespace Core;

public interface ISecretProtector
{
    byte[] Protect(ReadOnlySpan<byte> plaintext, string purpose);
    byte[] Unprotect(ReadOnlySpan<byte> ciphertext, string purpose);
    string ProtectString(string plaintext, string purpose);
    string UnprotectString(string ciphertext, string purpose);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface INodeScheduler
{
    Task<Node?> PickNodeAsync(string kind, CancellationToken ct);
}

public interface IContainerDispatcher
{
    Task<string> StartAsync(Instance instance, byte[] algoBytes, string paramJson, CancellationToken ct);
    Task StopAsync(Instance instance, CancellationToken ct);
    IAsyncEnumerable<string> TailLogsAsync(Instance instance, CancellationToken ct);
    Task<NodeStats> CollectStatsAsync(Node node, CancellationToken ct);
    Task<long> GetBacktestDataSizeAsync(Node node, CancellationToken ct);
    Task CleanBacktestDataAsync(Node node, UserId? userId, CancellationToken ct);

    /// <summary>Null when the container no longer exists (already removed).</summary>
    Task<bool?> IsRunningAsync(Instance instance, CancellationToken ct);

    /// <summary>Exit code of the (stopped) container, or null when it cannot be determined.</summary>
    Task<int?> GetExitCodeAsync(Instance instance, CancellationToken ct);
    Task<string?> ReadReportAsync(Instance instance, CancellationToken ct);
}

public interface IContainerDispatcherFactory
{
    IContainerDispatcher For(Node node);
    IContainerDispatcher For(Instance instance);
}

public interface IGithubContainerRegistryTagProvider
{
    Task<IReadOnlyList<string>> GetTagsAsync(CancellationToken ct);
}

public interface ICurrentUser
{
    UserId? UserId { get; }
    string? RoleName { get; }
    int? RoleRank { get; }
    string? Email { get; }
    bool IsInRole(string roleName);
    bool IsAtLeast(string roleName);
}
