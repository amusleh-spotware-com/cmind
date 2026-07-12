using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core.Calendar;

/// <summary>The least-privilege scopes a Calendar API client token may carry.</summary>
public static class CalendarScopes
{
    public const string Read = "calendar:read";
    public const string Blackout = "calendar:blackout";
    public const string Surprises = "calendar:surprises";
    public const string Stream = "calendar:stream";

    /// <summary>Reads the AI macro currency-strength read model (ranking + forward pair-outlook matrix).
    /// Rides the same JWT/rate-limit machinery as the calendar; gated additionally on <c>FeatureFlag.Ai</c>.</summary>
    public const string MarketRead = "market:read";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { Read, Blackout, Surprises, Stream, MarketRead };
}

/// <summary>
/// A registered consumer of the Calendar REST API (mirrors <c>McpApiKey</c>). An admin issues one with a
/// name, a set of scopes and an optional expiry; the client exchanges its id + secret for a short-lived JWT.
/// The secret is stored only as a hash (never plaintext). Disabling the client stops future token issuance
/// immediately, and the short JWT lifetime bounds a leaked token's blast radius.
/// </summary>
public sealed class CalendarApiClient : AuditedEntity<CalendarApiClientId>
{
    public UserId OwnerId { get; private set; }
    [MaxLength(128)] public string Name { get; private set; } = default!;
    [MaxLength(256)] public string ScopesCsv { get; private set; } = default!;
    [MaxLength(32)] public string KeyPrefix { get; private set; } = default!;
    [MaxLength(128)] public string KeyHash { get; private set; } = default!;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }

    private CalendarApiClient()
    {
    }

    public IReadOnlyList<string> Scopes =>
        ScopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static CalendarApiClient Create(
        UserId ownerId,
        string name,
        IReadOnlyCollection<string> scopes,
        string keyPrefix,
        string keyHash,
        DateTimeOffset? expiresAt)
    {
        if (scopes.Count == 0) throw new DomainException(DomainErrors.CalendarApiScopeInvalid);
        foreach (var scope in scopes)
            if (!CalendarScopes.All.Contains(scope))
                throw new DomainException(DomainErrors.CalendarApiScopeInvalid);

        return new CalendarApiClient
        {
            OwnerId = ownerId,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.CalendarApiClientNameRequired),
            ScopesCsv = string.Join(',', scopes),
            KeyPrefix = keyPrefix,
            KeyHash = keyHash,
            ExpiresAt = expiresAt
        };
    }

    public void Disable(DateTimeOffset now) => DisabledAt ??= now;

    public bool IsActive(DateTimeOffset now) =>
        DisabledAt is null && (ExpiresAt is not { } expiry || expiry > now);

    public bool HasScope(string scope) => Scopes.Contains(scope);
}
