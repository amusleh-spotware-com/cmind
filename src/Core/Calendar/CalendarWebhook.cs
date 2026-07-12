using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core.Calendar;

/// <summary>
/// A registered outbound webhook: cMind POSTs matching released events to <see cref="Url"/>, HMAC-signed with
/// the caller's shared secret (stored encrypted, never plaintext). Optionally narrowed to a minimum impact
/// and a currency set. Disabling stops future delivery. Owned by the API client's owner.
/// </summary>
public sealed class CalendarWebhook : AuditedEntity<CalendarWebhookId>
{
    public UserId OwnerId { get; private set; }
    [MaxLength(2048)] public string Url { get; private set; } = default!;
    public byte[] EncryptedSecret { get; private set; } = default!;
    [MaxLength(16)] public string? MinImpactLevel { get; private set; }
    [MaxLength(64)] public string? Currencies { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }

    private CalendarWebhook()
    {
    }

    public ImpactLevel MinImpact =>
        Enum.TryParse<ImpactLevel>(MinImpactLevel, out var level) ? level : ImpactLevel.Low;

    public static CalendarWebhook Create(
        UserId ownerId, string url, byte[] encryptedSecret, ImpactLevel minImpact, string? currencies)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new DomainException(DomainErrors.CalendarWebhookUrlInvalid);

        return new CalendarWebhook
        {
            OwnerId = ownerId,
            Url = url,
            EncryptedSecret = encryptedSecret,
            MinImpactLevel = minImpact.ToString(),
            Currencies = string.IsNullOrWhiteSpace(currencies) ? null : currencies.Trim()
        };
    }

    public void Disable(DateTimeOffset now) => DisabledAt ??= now;

    public bool IsActive => DisabledAt is null;

    /// <summary>Whether an event at <paramref name="impact"/> for <paramref name="currency"/> matches this hook's filter.</summary>
    public bool Matches(ImpactLevel impact, IReadOnlyCollection<string> eventCurrencies)
    {
        if (impact < MinImpact) return false;
        if (string.IsNullOrWhiteSpace(Currencies)) return true;
        var wanted = Currencies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var w in wanted)
            foreach (var c in eventCurrencies)
                if (string.Equals(w, c, StringComparison.OrdinalIgnoreCase))
                    return true;
        return false;
    }
}
