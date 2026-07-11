namespace Core.CopyTrading;

/// <summary>
/// Reads a destination account's current equity for performance-fee settlement. The implementation lives in
/// the infrastructure layer (resolves the account's credentials, opens an Open API session, computes
/// equity); the settlement service depends on this abstraction so its high-water-mark logic is exercised
/// without a live broker. Returns null when the account's equity cannot be read (unlinked / unauthorized).
/// </summary>
public interface ICopyEquityReader
{
    Task<double?> ReadEquityAsync(long destinationCtidTraderAccountId, CancellationToken ct);
}
