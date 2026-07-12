using Core.Constants;
using Core.Domain;

namespace Core.Accounts;

/// <summary>
/// A trading account's broker as a value object: trimmed, non-empty, capped at the persisted length, and
/// compared case-insensitively so allowlist matching, verification, and display all share one spelling of
/// "broker". Wrapping the primitive keeps the rule in one place rather than scattered across endpoints.
/// </summary>
public readonly record struct BrokerName
{
    public const int MaxLength = 128;

    public string Value { get; }

    public BrokerName(string value)
    {
        var trimmed = DomainGuard.AgainstNullOrWhiteSpace(value, DomainErrors.BrokerNameRequired);
        Value = trimmed.Length > MaxLength ? trimmed[..MaxLength] : trimmed;
    }

    public bool Equals(BrokerName other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;
}
