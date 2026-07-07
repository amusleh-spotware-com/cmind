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
}
