namespace Core.NodeAgent;

/// <summary>Request the main node sends to an external node agent to start a container.</summary>
public sealed record StartContainerRequest(
    Guid InstanceId,
    Guid UserId,
    string Kind,
    string Image,
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string> Files);

public sealed record StartContainerResponse(string ContainerId, string WorkDir);

public sealed record ContainerStatusResponse(bool Exists, bool Running, int? ExitCode);

public sealed record NodeStatsResponse(
    double CpuPercent,
    long MemUsedBytes,
    long MemTotalBytes,
    long DiskUsedBytes,
    long DiskTotalBytes,
    long BacktestDataUsedBytes);
