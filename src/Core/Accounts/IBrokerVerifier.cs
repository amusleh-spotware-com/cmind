namespace Core.Accounts;

/// <summary>
/// Verifies which broker a set of cID credentials + account number actually belongs to by asking the
/// cTrader platform — the infrastructure implementation runs the shipped broker-probe cBot through the
/// cTrader CLI and reads <c>Account.BrokerName</c> from its output. Only consulted when the deployment's
/// <see cref="BrokerAllowlist"/> is restricted; the manual-cID add path trusts the verified name over
/// anything the user typed. Core declares the port; the container mechanics live outside Core.
/// </summary>
public interface IBrokerVerifier
{
    Task<BrokerVerificationResult> VerifyAsync(BrokerProbeRequest request, CancellationToken ct);
}

/// <summary>
/// Inputs for a broker probe. Carries the cID password still <b>encrypted</b> (the verifier decrypts via
/// <c>ISecretProtector</c>) so plaintext never crosses this Core contract.
/// </summary>
public sealed record BrokerProbeRequest(
    string CtidUsername,
    byte[] EncryptedPassword,
    long AccountNumber,
    bool IsLive);

public enum BrokerVerificationError
{
    None,
    LoginFailed,
    Timeout,
    NoNodeAvailable,
    ProbeFailed
}

public sealed record BrokerVerificationResult(bool Success, BrokerName? Broker, BrokerVerificationError Error)
{
    public static BrokerVerificationResult Verified(BrokerName broker) =>
        new(true, broker, BrokerVerificationError.None);

    public static BrokerVerificationResult Failed(BrokerVerificationError error) =>
        new(false, null, error);
}
