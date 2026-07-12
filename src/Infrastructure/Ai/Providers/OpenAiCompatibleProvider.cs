using System.Net.Http.Json;
using System.Text.Json;
using Core.Ai;
using Core.Constants;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.Providers;

/// <summary>
/// OpenAI Chat Completions adapter (<c>{base}/chat/completions</c>, <c>Authorization: Bearer</c>,
/// <c>choices[0].message.content</c>). This one adapter covers every OpenAI-compatible target — cloud
/// (OpenAI, Mistral, Groq, Together, OpenRouter, DeepSeek) <b>and</b> every local runtime (Ollama,
/// LM Studio, vLLM, llama.cpp, LocalAI). The key is optional (omitted for keyless local endpoints);
/// the system role is folded into the first user turn when the target can't take a system message.
/// </summary>
public sealed class OpenAiCompatibleProvider(HttpClient http, ILogger<OpenAiCompatibleProvider> logger) : IAiProvider
{
    public AiProviderKind Kind => AiProviderKind.OpenAiCompatible;

    public async Task<AiResult> CompleteAsync(AiProviderRequest request, CancellationToken ct)
    {
        var body = BuildBody(request);
        using var msg = new HttpRequestMessage(
            HttpMethod.Post, new Uri(new Uri(request.BaseUrl), OpenAiConstants.ChatCompletionsPath));
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
            msg.Headers.Add(OpenAiConstants.AuthorizationHeader, OpenAiConstants.BearerPrefix + request.ApiKey);
        msg.Content = JsonContent.Create(body);

        return await AiWireHelpers.SendAsync(http, msg, ExtractText, logger, ct);
    }

    // Shared with the Azure adapter (same body + response shape; only auth header + path differ).
    internal static Dictionary<string, object?> BuildBody(AiProviderRequest request)
    {
        object userContent = request.Image is { } img
            ? new object[]
            {
                new { type = "text", text = request.User },
                new { type = "image_url", image_url = new { url = $"data:{img.MediaType};base64,{img.Base64Data}" } }
            }
            : request.User;

        var messages = new List<object>();
        if (request.Capabilities.SupportsSystemRole)
        {
            if (!string.IsNullOrWhiteSpace(request.System))
                messages.Add(new { role = "system", content = request.System });
            messages.Add(new { role = "user", content = userContent });
        }
        else
        {
            // Fold the system prompt into the user turn for chat templates without a system role.
            if (request.Image is null)
                messages.Add(new { role = "user", content = FoldSystem(request.System, request.User) });
            else
                messages.Add(new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = FoldSystem(request.System, request.User) },
                        new { type = "image_url", image_url = new { url = $"data:{request.Image.MediaType};base64,{request.Image.Base64Data}" } }
                    }
                });
        }

        return new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens,
            ["messages"] = messages
        };
    }

    internal static string ExtractText(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
            return string.Empty;

        var message = choices[0].TryGetProperty("message", out var m) ? m : default;
        if (message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("content", out var content))
            return string.Empty;

        // content is usually a string; some servers return an array of parts.
        if (content.ValueKind == JsonValueKind.String) return content.GetString()?.Trim() ?? string.Empty;
        if (content.ValueKind == JsonValueKind.Array)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var part in content.EnumerateArray())
                if (part.TryGetProperty("text", out var t)) sb.Append(t.GetString());
            return sb.ToString().Trim();
        }
        return string.Empty;
    }

    private static string FoldSystem(string system, string user)
        => string.IsNullOrWhiteSpace(system) ? user : $"{system}\n\n{user}";
}
