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

/// <summary>Agent self-report used for version/compatibility handshakes and diagnostics.</summary>
public sealed record NodeAgentInfoResponse(string ProductVersion, int ProtocolVersion);

public sealed record ContainerStatusResponse(bool Exists, bool Running, int? ExitCode);

/// <summary>Self-registration + heartbeat an agent pushes to the main node's discovery endpoint.</summary>
public sealed record NodeRegistrationRequest(
    string Name,
    string BaseUrl,
    string Mode,
    int MaxInstances,
    string DataDirPath,
    int ProtocolVersion);

public sealed record NodeRegistrationResponse(Guid NodeId, int HeartbeatIntervalSeconds);

public sealed record NodeStatsResponse(
    double CpuPercent,
    long MemUsedBytes,
    long MemTotalBytes,
    long DiskUsedBytes,
    long DiskTotalBytes,
    long BacktestDataUsedBytes);
