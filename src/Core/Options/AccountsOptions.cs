using Core.Constants;

namespace Core.Options;

/// <summary>
/// White-label account policy, bound from <c>App:Accounts</c>. When <see cref="AllowedBrokers"/> is
/// non-empty the deployment only accepts trading accounts from those brokers — checked on the cTrader
/// Open API link and <b>verified</b> on manual cID add (by probing the account's real broker name). Empty
/// (the default) allows every broker and runs no verification, so a stock deployment is unchanged.
/// </summary>
public sealed record AccountsOptions
{
    public IReadOnlyList<string> AllowedBrokers { get; init; } = [];

    /// <summary>How long the broker-probe container may run before verification is failed as a timeout.</summary>
    public TimeSpan BrokerProbeTimeout { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Filesystem path (on the web host) to the prebuilt broker-probe <c>.algo</c> the verifier runs to read
    /// <c>Account.BrokerName</c>. Ships with the deployment; source lives in <c>tools/broker-probe/</c>. When
    /// unset or missing, manual-cID broker verification fails closed (accounts under a restricted allowlist
    /// can still be linked via the Open API path, which needs no probe).
    /// </summary>
    public string BrokerProbeAlgoPath { get; init; } = FilePaths.BrokerProbeAlgoDefault;
}
