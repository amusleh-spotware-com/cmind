namespace Core.Accounts;

/// <summary>
/// The set of brokers a white-label deployment permits trading accounts from. Empty = unrestricted (every
/// broker allowed) — the default, so a stock deployment performs no broker checks at all. Matching is
/// case-insensitive via <see cref="BrokerName"/>.
/// </summary>
public sealed class BrokerAllowlist
{
    private readonly HashSet<BrokerName> _allowed;

    public static BrokerAllowlist Unrestricted { get; } = new([]);

    public BrokerAllowlist(IEnumerable<BrokerName> allowed) => _allowed = [.. allowed];

    public bool IsRestricted => _allowed.Count > 0;

    public IReadOnlyCollection<BrokerName> Allowed => _allowed;

    public bool Allows(BrokerName broker) => !IsRestricted || _allowed.Contains(broker);

    /// <summary>Builds an allowlist from raw config strings, skipping blank entries and de-duplicating.</summary>
    public static BrokerAllowlist FromNames(IEnumerable<string> names)
    {
        var allowed = new List<BrokerName>();
        foreach (var name in names)
            if (!string.IsNullOrWhiteSpace(name))
                allowed.Add(new BrokerName(name));
        return new BrokerAllowlist(allowed);
    }
}
