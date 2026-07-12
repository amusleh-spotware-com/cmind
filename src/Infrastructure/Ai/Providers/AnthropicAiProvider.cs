using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Core.Ai;
using Core.Constants;
using Core.Logging;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.Providers;

/// <summary>
/// Anthropic Messages API adapter (<c>v1/messages</c>, <c>x-api-key</c>, <c>anthropic-version</c>,
/// server-side <c>web_search</c> tool, base64 image block, <c>content[].text</c> response).
/// </summary>
public sealed class AnthropicAiProvider(HttpClient http, ILogger<AnthropicAiProvider> logger) : IAiProvider
{
    public AiProviderKind Kind => AiProviderKind.Anthropic;

    public async Task<AiResult> CompleteAsync(AiProviderRequest request, CancellationToken ct)
    {
        object userContent = request.Image is { } img
            ? new object[]
            {
                new { type = "image", source = new { type = "base64", media_type = img.MediaType, data = img.Base64Data } },
                new { type = "text", text = request.User }
            }
            : request.User;

        var body = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens,
            ["system"] = request.System,
            ["messages"] = new[] { new { role = "user", content = userContent } }
        };
        if (request.EnableWebSearch)
            body["tools"] = new[] { new { type = AiConstants.WebSearchToolType, name = AiConstants.WebSearchToolName } };

        using var msg = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(request.BaseUrl), AiConstants.MessagesPath));
        if (!string.IsNullOrWhiteSpace(request.ApiKey)) msg.Headers.Add(AiConstants.ApiKeyHeader, request.ApiKey);
        msg.Headers.Add(AiConstants.AnthropicVersionHeader, AiConstants.AnthropicVersion);
        msg.Content = JsonContent.Create(body);

        return await AiWireHelpers.SendAsync(http, msg, ExtractText, logger, ct);
    }

    private static string ExtractText(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) && t.GetString() == "text"
                                                        && block.TryGetProperty("text", out var txt))
                sb.Append(txt.GetString());
        }
        return sb.ToString().Trim();
    }
}
