namespace Core.Domain;

public sealed record AuditChainStatus(bool Intact, long Count, long? FirstBrokenId);

/// <summary>Re-walks the audit-log hash chain to detect any tampering (edited or deleted past records).</summary>
public interface IAuditTrailVerifier
{
    Task<AuditChainStatus> VerifyAsync(CancellationToken ct);
}
