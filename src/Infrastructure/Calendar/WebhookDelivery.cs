using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Calendar;

/// <summary>
/// Delivers a JSON payload to a registered webhook URL, signed with the caller's shared secret so the
/// receiver can verify authenticity: an <c>X-CMind-Signature: sha256=&lt;hex&gt;</c> header carrying the
/// HMAC-SHA256 of the exact body. Best-effort — returns whether the endpoint accepted it (a caller schedules
/// retries); behind a resilient typed <c>HttpClient</c>.
/// </summary>
public sealed class WebhookDelivery(HttpClient httpClient)
{
    public const string SignatureHeader = "X-CMind-Signature";

    /// <summary>The HMAC-SHA256 hex of <paramref name="payload"/> under <paramref name="secret"/>.</summary>
    public static string Sign(string secret, string payload)
        => Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    public async Task<bool> DeliverAsync(string url, string secret, string payload, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation(SignatureHeader, $"sha256={Sign(secret, payload)}");

            using var response = await httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
