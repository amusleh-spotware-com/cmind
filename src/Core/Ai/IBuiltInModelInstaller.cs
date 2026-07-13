namespace Core.Ai;

/// <summary>Lifecycle of the one-time background download of the built-in ONNX model.</summary>
public enum BuiltInModelInstallState
{
    NotStarted,
    Downloading,
    Installed,
    Failed
}

/// <summary>
/// Port that installs the built-in local LLM model on demand: when the model directory is absent and
/// auto-download is enabled, a single background download fetches the model files so the built-in AI works
/// out of the box with no manual provisioning. Implementations are single-flight (idempotent) and never
/// throw on the request path — the provider degrades to a typed "downloading" failure meanwhile.
/// </summary>
public interface IBuiltInModelInstaller
{
    /// <summary>True once the model directory is present and usable.</summary>
    bool IsInstalled();

    /// <summary>Current state of the (at-most-one) background download.</summary>
    BuiltInModelInstallState State { get; }

    /// <summary>Starts the background download if it is not already running/installed. Returns immediately.</summary>
    void EnsureInstalling();
}
