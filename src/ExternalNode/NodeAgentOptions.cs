namespace ExternalNode;

public sealed class NodeAgentOptions
{
    public const string SectionName = "NodeAgent";

    /// <summary>Shared HS256 secret; the main node signs request JWTs with the same value.</summary>
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>Host directory under which per-instance work dirs are created.</summary>
    public string DataRoot { get; set; } = "/var/app/data";

    public string DockerPath { get; set; } = "docker";

    /// <summary>Only images whose reference starts with this prefix may be run.</summary>
    public string AllowedImagePrefix { get; set; } = "ghcr.io/spotware/";

    public int PullTimeoutSeconds { get; set; } = 600;
    public int ProcessTimeoutSeconds { get; set; } = 60;
    public long MaxFileBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>Main node base URL to self-register against. Empty = manual registration mode.</summary>
    public string MainUrl { get; set; } = string.Empty;

    /// <summary>URL the main node uses to reach THIS agent (e.g. the pod/service address).</summary>
    public string AdvertiseUrl { get; set; } = string.Empty;

    /// <summary>Unique node name; the main node keys the registration by this.</summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>Run / Backtest / Mixed.</summary>
    public string Mode { get; set; } = "Mixed";

    public int MaxInstances { get; set; } = 10;

    public int HeartbeatIntervalSeconds { get; set; } = 30;
}
