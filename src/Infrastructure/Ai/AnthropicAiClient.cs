using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Core.Ai;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai;

public sealed class AnthropicAiClient(
    HttpClient http,
    IOptionsMonitor<AppOptions> options,
    IAiKeyStore keyStore,
    ILogger<AnthropicAiClient> logger) : IAiClient
{
    private AiOptions Options => options.CurrentValue.Ai;

    public bool Enabled => keyStore.HasKey;

    public async Task<AiResult> CompleteAsync(AiTextRequest request, CancellationToken ct)
    {
        var ai = Options;
        if (keyStore.CurrentKey is not { } apiKey) return AiResult.Fail(AiConstants.DisabledMessage);

        object userContent = request.Image is { } img
            ? new object[]
            {
                new { type = "image", source = new { type = "base64", media_type = img.MediaType, data = img.Base64Data } },
                new { type = "text", text = request.User }
            }
            : request.User;

        var body = new Dictionary<string, object?>
        {
            ["model"] = ai.Model,
            ["max_tokens"] = request.MaxTokens ?? ai.MaxTokens,
            ["system"] = request.System,
            ["messages"] = new[] { new { role = "user", content = userContent } }
        };
        if (request.EnableWebSearch)
            body["tools"] = new[] { new { type = AiConstants.WebSearchToolType, name = AiConstants.WebSearchToolName } };

        using var msg = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(ai.BaseUrl), AiConstants.MessagesPath));
        msg.Headers.Add(AiConstants.ApiKeyHeader, apiKey);
        msg.Headers.Add(AiConstants.AnthropicVersionHeader, AiConstants.AnthropicVersion);
        msg.Content = JsonContent.Create(body);

        try
        {
            using var resp = await http.SendAsync(msg, ct);
            var payload = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.AiRequestFailed((int)resp.StatusCode, Truncate(payload, 500));
                return AiResult.Fail($"AI request failed ({(int)resp.StatusCode}).");
            }

            var text = ExtractText(payload);
            return string.IsNullOrWhiteSpace(text)
                ? AiResult.Fail("AI returned an empty response.")
                : AiResult.Ok(text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.AiRequestError(ex);
            return AiResult.Fail("AI request errored.");
        }
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

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
