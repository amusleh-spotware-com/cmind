using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CTraderOpenApi.Auth;

public sealed record OpenApiTokenResponse(string AccessToken, string RefreshToken, long ExpiresInSeconds, string TokenType);

public interface IOpenApiTokenClient
{
    Task<OpenApiTokenResponse> ExchangeCodeAsync(
        string clientId, string clientSecret, string code, string redirectUri, CancellationToken ct);

    Task<OpenApiTokenResponse> RefreshAsync(
        string clientId, string clientSecret, string refreshToken, CancellationToken ct);
}

public sealed class OpenApiTokenClient(HttpClient http) : IOpenApiTokenClient
{
    private const string TokenPath = "apps/token";

    public Task<OpenApiTokenResponse> ExchangeCodeAsync(
        string clientId, string clientSecret, string code, string redirectUri, CancellationToken ct)
        => RequestAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        }, ct);

    public Task<OpenApiTokenResponse> RefreshAsync(
        string clientId, string clientSecret, string refreshToken, CancellationToken ct)
        => RequestAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        }, ct);

    private async Task<OpenApiTokenResponse> RequestAsync(IReadOnlyDictionary<string, string> parameters, CancellationToken ct)
    {
        var query = string.Join('&', parameters.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        using var response = await http.GetAsync($"{TokenPath}?{query}", ct);
        var payload = await response.Content.ReadFromJsonAsync<TokenPayload>(ct);

        if (payload is null || !string.IsNullOrEmpty(payload.ErrorCode) || string.IsNullOrEmpty(payload.AccessToken))
            throw new OpenApiException(new OpenApiError(
                payload?.ErrorCode ?? "TOKEN_REQUEST_FAILED", payload?.Description, OpenApiErrorKind.Fatal, null));

        return new OpenApiTokenResponse(
            payload.AccessToken!,
            payload.RefreshToken ?? string.Empty,
            payload.ExpiresIn,
            payload.TokenType ?? "bearer");
    }

    private sealed record TokenPayload(
        [property: JsonPropertyName("accessToken")] string? AccessToken,
        [property: JsonPropertyName("refreshToken")] string? RefreshToken,
        [property: JsonPropertyName("expiresIn")] long ExpiresIn,
        [property: JsonPropertyName("tokenType")] string? TokenType,
        [property: JsonPropertyName("errorCode")] string? ErrorCode,
        [property: JsonPropertyName("description")] string? Description);
}
