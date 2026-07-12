using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Infrastructure.Calendar;

/// <summary>The blackout answer a cBot acts on. On any uncertainty it is fail-safe (treat as in-blackout).</summary>
public sealed record CalendarBlackout(bool InBlackout, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, bool Stale);

/// <summary>
/// A tiny typed client for the public Calendar REST API — the shipped snippet a cBot author points at their
/// deployment. Exchange a client id + secret for a short JWT, then ask the blackout question before placing an
/// order. Fail-safe by construction: any non-success/parse error yields an in-blackout answer, so a data gap
/// never green-lights trading through a release. Give the underlying <c>HttpClient</c> a base address of the
/// API root (e.g. <c>https://host/api/calendar/</c>) and, ideally, a Polly resilience handler.
/// </summary>
public sealed class CmindCalendarClient(HttpClient httpClient)
{
    /// <summary>Exchanges a client id + secret for a short-lived bearer token; <c>null</c> on failure.</summary>
    public async Task<string?> GetTokenAsync(string clientId, string clientSecret, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "v1/token", new { clientId, clientSecret }, ct);
        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return body.TryGetProperty("token", out var token) ? token.GetString() : null;
    }

    /// <summary>Asks whether a symbol is inside a high-impact news window; fail-safe to in-blackout on any error.</summary>
    public async Task<CalendarBlackout> GetBlackoutAsync(
        string token, string symbol, string minImpact = "High", int before = 15, int after = 15,
        CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"v1/blackout?symbol={Uri.EscapeDataString(symbol)}&minImpact={minImpact}&before={before}&after={after}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return new CalendarBlackout(InBlackout: true, null, null, Stale: true);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var inBlackout = body.TryGetProperty("inBlackout", out var flag) && flag.GetBoolean();
            var startsAt = body.TryGetProperty("startsAt", out var s) && s.ValueKind is not JsonValueKind.Null
                ? s.GetDateTimeOffset() : (DateTimeOffset?)null;
            var endsAt = body.TryGetProperty("endsAt", out var e) && e.ValueKind is not JsonValueKind.Null
                ? e.GetDateTimeOffset() : (DateTimeOffset?)null;
            return new CalendarBlackout(inBlackout, startsAt, endsAt, Stale: false);
        }
        catch (Exception)
        {
            return new CalendarBlackout(InBlackout: true, null, null, Stale: true);
        }
    }
}
