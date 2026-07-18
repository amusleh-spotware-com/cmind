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
    /// <summary>True once the default model directory is present and usable.</summary>
    bool IsInstalled();

    /// <summary>Current state of the default model's background download.</summary>
    BuiltInModelInstallState State { get; }

    /// <summary>Starts the default model's background download if not already running/installed. Returns immediately.</summary>
    void EnsureInstalling();

    /// <summary>True once the given curated model (by <see cref="BuiltInModelSpec.Key"/>) is present and usable.</summary>
    bool IsInstalled(string key);

    /// <summary>Current state of the given curated model's background download.</summary>
    BuiltInModelInstallState StateOf(string key);

    /// <summary>Starts the given curated model's background download if not already running/installed.</summary>
    void EnsureInstalling(string key);

    /// <summary>Every curated built-in model with its current install state, for the management UI.</summary>
    IReadOnlyList<BuiltInModelStatus> Catalog();
}
