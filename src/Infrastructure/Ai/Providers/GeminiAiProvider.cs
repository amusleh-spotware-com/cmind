using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Core.Ai;
using Core.Constants;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.Providers;

/// <summary>
/// Google Gemini adapter (<c>{base}v1beta/models/{model}:generateContent?key=</c>,
/// <c>systemInstruction</c> + <c>contents[].parts[]</c>, inline_data for vision, the
/// <c>google_search</c> grounding tool when enabled, <c>candidates[0].content.parts[].text</c>).
/// </summary>
public sealed class GeminiAiProvider(HttpClient http, ILogger<GeminiAiProvider> logger) : IAiProvider
{
    public AiProviderKind Kind => AiProviderKind.Gemini;

    public async Task<AiResult> CompleteAsync(AiProviderRequest request, CancellationToken ct)
    {
        var parts = request.Image is { } img
            ? new object[]
            {
                new { text = request.User },
                new { inline_data = new { mime_type = img.MediaType, data = img.Base64Data } }
            }
            : [new { text = request.User }];

        var body = new Dictionary<string, object?>
        {
            ["contents"] = new[] { new { role = "user", parts } }
        };
        if (!string.IsNullOrWhiteSpace(request.System))
            body["systemInstruction"] = new { parts = new[] { new { text = request.System } } };
        if (request.EnableWebSearch)
            body["tools"] = new[] { new Dictionary<string, object?> { [GeminiConstants.GoogleSearchTool] = new { } } };

        var path = GeminiConstants.GenerateContentPath(request.Model);
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
            path += $"?{GeminiConstants.KeyQuery}={request.ApiKey}";

        using var msg = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(request.BaseUrl), path));
        msg.Content = JsonContent.Create(body);

        return await AiWireHelpers.SendAsync(http, msg, ExtractText, logger, ct);
    }

    private static string ExtractText(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
            return string.Empty;

        if (!candidates[0].TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
            if (part.TryGetProperty("text", out var t)) sb.Append(t.GetString());
        return sb.ToString().Trim();
    }
}
