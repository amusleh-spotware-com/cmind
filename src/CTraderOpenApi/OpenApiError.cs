namespace CTraderOpenApi;

public enum OpenApiErrorKind
{
    Recoverable,
    Fatal,
    TokenInvalid,
    Maintenance
}

public sealed record OpenApiError(
    string Code,
    string? Description,
    OpenApiErrorKind Kind,
    DateTimeOffset? MaintenanceEndsAt)
{
    private static readonly HashSet<string> FatalCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "INVALID_REQUEST",
        "UNSUPPORTED_MESSAGE",
        "CH_CLIENT_AUTH_FAILURE",
        "CH_APPLICATION_NOT_AUTHORIZED",
        "CH_CTID_TRADER_ACCOUNT_NOT_FOUND"
    };

    private static readonly HashSet<string> TokenCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CH_ACCESS_TOKEN_INVALID",
        "OA_AUTH_TOKEN_EXPIRED",
        "CH_OA_AUTH_TOKEN_EXPIRED",
        "ACCESS_TOKEN_EXPIRED",
        "TOKEN_EXPIRED"
    };

    public static OpenApiError Classify(string code, string? description, DateTimeOffset? maintenanceEndsAt)
    {
        if (maintenanceEndsAt is not null)
            return new OpenApiError(code, description, OpenApiErrorKind.Maintenance, maintenanceEndsAt);

        if (TokenCodes.Contains(code))
            return new OpenApiError(code, description, OpenApiErrorKind.TokenInvalid, null);

        var kind = FatalCodes.Contains(code) ? OpenApiErrorKind.Fatal : OpenApiErrorKind.Recoverable;
        return new OpenApiError(code, description, kind, null);
    }
}

public sealed class OpenApiException(OpenApiError error)
    : Exception($"cTrader Open API error {error.Code}: {error.Description}")
{
    public OpenApiError Error { get; } = error;
}
