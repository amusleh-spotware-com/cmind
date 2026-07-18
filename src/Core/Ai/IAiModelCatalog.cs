namespace Core.Ai;

/// <summary>One model a provider endpoint advertises — the id the user selects into a credential's Model
/// field, plus optional display metadata. <paramref name="Family"/> is a coarse label (e.g. the owner /
/// series) when the endpoint exposes one; <paramref name="SizeBytes"/> is the on-disk size for local
/// models when known.</summary>
public sealed record AiModelInfo(string Id, string? Family = null, long? SizeBytes = null);

/// <summary>
/// Discovers the models an AI provider makes available so the user can browse and pick one instead of
/// hand-typing a model id. OpenAI-compatible endpoints (LM Studio, Ollama, vLLM, llama.cpp, LocalAI, and
/// the cloud OpenAI-compatible services) are queried over <c>GET {base}/models</c>; the built-in ONNX
/// provider enumerates the installed local model directories; Anthropic/Gemini use their list endpoints.
/// Always degrades to an empty list (never throws) so a dead endpoint or missing key just shows nothing.
/// </summary>
public interface IAiModelCatalog
{
    Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(
        AiProviderKind kind, AiEndpoint baseUrl, string? apiKey, CancellationToken ct);
}
