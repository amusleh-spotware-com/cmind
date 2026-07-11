using Core;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class AuditTrailVerifier(DataContext db) : IAuditTrailVerifier
{
    public async Task<AuditChainStatus> VerifyAsync(CancellationToken ct)
    {
        // Id ascending == insertion order == the order AuditChainInterceptor built the chain in.
        var entries = await db.AuditLogs.AsNoTracking().OrderBy(a => a.Id).ToListAsync(ct);

        string? prev = null;
        foreach (var entry in entries)
        {
            if (entry.PrevHash != prev || entry.Hash != entry.ExpectedHash(prev))
                return new AuditChainStatus(false, entries.Count, entry.Id);
            prev = entry.Hash;
        }

        return new AuditChainStatus(true, entries.Count, null);
    }
}
