using Core.Constants;

namespace Core.Ai;

/// <summary>One curated, ONNX-GenAI-format local model the app can download on demand. <paramref name="Key"/>
/// is the stable id (also the install sub-directory name and the credential <c>Model</c> value used to
/// select it); <paramref name="IsDefault"/> marks the model shipped/seeded out of the box, which installs at
/// the configured <c>ModelPath</c> root, while every other model installs under <c>ModelPath/&lt;Key&gt;</c>
/// so several can coexist.</summary>
public sealed record BuiltInModelSpec(
    string Key,
    string DisplayName,
    string DownloadBaseUrl,
    IReadOnlyList<string> Files,
    bool IsDefault);

/// <summary>A curated built-in model plus its current install state, for the management UI.</summary>
public sealed record BuiltInModelStatus(BuiltInModelSpec Spec, bool Installed, BuiltInModelInstallState State);

/// <summary>
/// The curated set of built-in local LLMs a user can download and switch between without configuring an
/// external provider. Every entry is a verified ONNX-GenAI (onnxruntime-genai) CPU int4 build carrying a
/// <c>genai_config.json</c>, so the in-process <c>OnnxGenAiProvider</c> can load it directly. The default is
/// Phi-3.5-mini-instruct (also the auto-download shipped out of the box); additional entries let a user pick
/// a different local model. Add an entry only after verifying the repo folder is GenAI-format.
/// </summary>
public static class BuiltInModelCatalog
{
    /// <summary>Shipped/seeded default — installs at the <c>ModelPath</c> root (auto-download target).</summary>
    public static readonly BuiltInModelSpec Default = new(
        Key: AiConstants.BuiltInModel,
        DisplayName: "Phi-3.5 Mini Instruct (CPU int4)",
        DownloadBaseUrl: AiConstants.BuiltInModelDownloadBaseUrl,
        Files: AiConstants.BuiltInModelDownloadFiles,
        IsDefault: true);

    // A second verified GenAI-format option (the previous shipped default). RTN int4, 128k context.
    private static readonly BuiltInModelSpec Phi3Mini128k = new(
        Key: "phi-3-mini-128k",
        DisplayName: "Phi-3 Mini 128k Instruct (CPU int4)",
        DownloadBaseUrl: "https://huggingface.co/microsoft/Phi-3-mini-128k-instruct-onnx/resolve/main/" +
                         "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/",
        Files:
        [
            "genai_config.json",
            "config.json",
            "special_tokens_map.json",
            "tokenizer.json",
            "tokenizer_config.json",
            "added_tokens.json",
            "phi3-mini-128k-instruct-cpu-int4-rtn-block-32-acc-level-4.onnx",
            "phi3-mini-128k-instruct-cpu-int4-rtn-block-32-acc-level-4.onnx.data"
        ],
        IsDefault: false);

    public static readonly IReadOnlyList<BuiltInModelSpec> All = [Default, Phi3Mini128k];

    /// <summary>The spec for a key, or the default when the key is blank/unknown.</summary>
    public static BuiltInModelSpec ForKey(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? Default
            : All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Default;
}
