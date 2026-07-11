using Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence;

/// <summary>
/// Transparently hash-chains every <see cref="AuditLog"/> as it is inserted, linking it to the previous
/// entry's hash. Existing audit call sites need no change; tamper-evidence is applied at persistence time.
/// </summary>
public sealed class AuditChainInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Chain(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Chain(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Chain(DbContext? context)
    {
        if (context is null) return;

        // Invariant: entries are chained in insertion order. AuditLog has no FK dependencies, so EF inserts
        // it in ChangeTracker add-order and the DB assigns Id in that same order — which is exactly the order
        // AuditTrailVerifier re-walks (Id ascending). Callers append one entry per SaveChanges; if a caller
        // ever adds several in one save, they are chained here in add-order to preserve that alignment.
        var added = context.ChangeTracker.Entries<AuditLog>()
            .Where(e => e.State == EntityState.Added && e.Entity.Hash is null)
            .Select(e => e.Entity)
            .ToList();
        if (added.Count == 0) return;

        var prev = context.Set<AuditLog>().AsNoTracking()
            .OrderByDescending(a => a.Id).Select(a => a.Hash).FirstOrDefault();
        foreach (var entry in added)
            prev = entry.ComputeHash(prev);
    }
}
