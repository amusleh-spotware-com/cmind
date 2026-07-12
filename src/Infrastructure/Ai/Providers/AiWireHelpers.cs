using Core.Ai;
using Core.Logging;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.Providers;

/// <summary>
/// Shared send + degrade path for every provider adapter: one HTTP round-trip, a wire-specific text
/// extractor, and the identical typed-failure degradation as before (non-2xx, empty, malformed, or a
/// thrown exception all become an <see cref="AiResult"/> failure — never an exception into a page,
/// tool, or hosted service). The resilience pipeline (timeouts + retry) is attached to the shared
/// <c>HttpClient</c>, so this is the same guarantee for cloud and local endpoints.
/// </summary>
internal static class AiWireHelpers
{
    public static async Task<AiResult> SendAsync(
        HttpClient http, HttpRequestMessage msg, Func<string, string> extractText, ILogger logger, CancellationToken ct)
    {
        try
        {
            using var resp = await http.SendAsync(msg, ct);
            var payload = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.AiRequestFailed((int)resp.StatusCode, Truncate(payload, 500));
                return AiResult.Fail($"AI request failed ({(int)resp.StatusCode}).");
            }

            var text = extractText(payload);
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

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
