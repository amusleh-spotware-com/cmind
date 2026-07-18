using System.Text.Json;
using Core.Ai;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai;

/// <summary>
/// Lists the models a provider endpoint advertises. OpenAI-compatible targets (LM Studio, Ollama, vLLM,
/// llama.cpp, LocalAI, and the cloud OpenAI-compatible services) answer <c>GET {base}/models</c>;
/// Anthropic and Gemini use their own list endpoints; the built-in ONNX provider enumerates the installed
/// local model directories. Every path degrades to an empty list on any failure (dead endpoint, wrong key,
/// malformed body) so the browse UI simply shows nothing instead of erroring — mirrors the adapters'
/// typed-failure contract.
/// </summary>
public sealed class AiModelCatalog(
    HttpClient http, IOptionsMonitor<AppOptions> options, ILogger<AiModelCatalog> logger) : IAiModelCatalog
{
    public async Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(
        AiProviderKind kind, AiEndpoint baseUrl, string? apiKey, CancellationToken ct)
    {
        try
        {
            return kind switch
            {
                AiProviderKind.OpenAiCompatible => await ListOpenAiCompatibleAsync(baseUrl, apiKey, ct),
                AiProviderKind.Anthropic => await ListAnthropicAsync(baseUrl, apiKey, ct),
                AiProviderKind.Gemini => await ListGeminiAsync(baseUrl, apiKey, ct),
                AiProviderKind.BuiltInOnnx => ListBuiltInOnnx(),
                AiProviderKind.Demo => [new AiModelInfo(AiConstants.DemoModel)],
                // Azure OpenAI is addressed by user-named deployments, not a discoverable model list.
                _ => []
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.AiRequestError(ex);
            return [];
        }
    }

    // GET {base}/models -> { "data": [ { "id": "..." }, ... ] }. Bearer key optional (keyless local).
    private async Task<IReadOnlyList<AiModelInfo>> ListOpenAiCompatibleAsync(
        AiEndpoint baseUrl, string? apiKey, CancellationToken ct)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUrl.ToUri(), "models"));
        if (!string.IsNullOrWhiteSpace(apiKey))
            msg.Headers.Add(OpenAiConstants.AuthorizationHeader, OpenAiConstants.BearerPrefix + apiKey);
        return await FetchAsync(msg, ParseDataIdArray, ct);
    }

    // GET {base}/v1/models -> { "data": [ { "id": "claude-..." }, ... ] }. x-api-key + version headers.
    private async Task<IReadOnlyList<AiModelInfo>> ListAnthropicAsync(
        AiEndpoint baseUrl, string? apiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return [];
        using var msg = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUrl.ToUri(), "v1/models"));
        msg.Headers.Add(AiConstants.ApiKeyHeader, apiKey);
        msg.Headers.Add(AiConstants.AnthropicVersionHeader, AiConstants.AnthropicVersion);
        return await FetchAsync(msg, ParseDataIdArray, ct);
    }

    // GET {base}/v1beta/models?key= -> { "models": [ { "name": "models/gemini-..." }, ... ] }.
    private async Task<IReadOnlyList<AiModelInfo>> ListGeminiAsync(
        AiEndpoint baseUrl, string? apiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return [];
        var uri = new Uri(baseUrl.ToUri(), $"v1beta/models?{GeminiConstants.KeyQuery}={Uri.EscapeDataString(apiKey)}");
        using var msg = new HttpRequestMessage(HttpMethod.Get, uri);
        return await FetchAsync(msg, ParseGeminiModels, ct);
    }

    // Enumerate installed built-in ONNX model directories (each folder with a genai_config.json is one
    // selectable local model). The configured ModelPath may itself be a model dir or a parent of several.
    private List<AiModelInfo> ListBuiltInOnnx()
    {
        var configured = options.CurrentValue.Ai.BuiltIn.ModelPath;
        var root = Path.IsPathRooted(configured) ? configured : Path.Combine(AppContext.BaseDirectory, configured);

        var models = new List<AiModelInfo>();
        if (IsModelDir(root)) models.Add(BuiltInModelInfo(root));
        if (Directory.Exists(root))
            foreach (var dir in Directory.EnumerateDirectories(root))
                if (IsModelDir(dir)) models.Add(BuiltInModelInfo(dir));

        return models;
    }

    private static bool IsModelDir(string dir) =>
        Directory.Exists(dir) && File.Exists(Path.Combine(dir, "genai_config.json"));

    private static AiModelInfo BuiltInModelInfo(string dir)
    {
        long? size = null;
        try
        {
            size = new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.TopDirectoryOnly).Sum(f => f.Length);
        }
        catch (Exception)
        {
            // Size is best-effort display metadata; omit it when the directory can't be measured.
        }
        return new AiModelInfo(Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar)), "ONNX", size);
    }

    private async Task<IReadOnlyList<AiModelInfo>> FetchAsync(
        HttpRequestMessage msg, Func<string, IReadOnlyList<AiModelInfo>> parse, CancellationToken ct)
    {
        using var resp = await http.SendAsync(msg, ct);
        if (!resp.IsSuccessStatusCode) return [];
        var payload = await resp.Content.ReadAsStringAsync(ct);
        return parse(payload);
    }

    private static IReadOnlyList<AiModelInfo> ParseDataIdArray(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return [];
        var models = new List<AiModelInfo>();
        foreach (var item in data.EnumerateArray())
            if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                && id.GetString() is { Length: > 0 } value)
                models.Add(new AiModelInfo(value));
        return models;
    }

    private static IReadOnlyList<AiModelInfo> ParseGeminiModels(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
            return [];
        var result = new List<AiModelInfo>();
        foreach (var item in models.EnumerateArray())
            if (item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                && name.GetString() is { Length: > 0 } value)
                // Gemini ids are namespaced as "models/gemini-...": strip the prefix to the bare id.
                result.Add(new AiModelInfo(value.StartsWith("models/", StringComparison.Ordinal) ? value[7..] : value));
        return result;
    }
}
