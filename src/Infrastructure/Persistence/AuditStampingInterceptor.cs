using Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence;

public sealed class AuditStampingInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    private const string CreatedAt = nameof(AuditedEntity<UserId>.CreatedAt);
    private const string UpdatedAt = nameof(AuditedEntity<UserId>.UpdatedAt);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;
        var now = timeProvider.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added
                && entry.Metadata.FindProperty(CreatedAt) is not null)
            {
                var created = entry.Property(CreatedAt);
                if (created.CurrentValue is DateTimeOffset dt && dt == default)
                    created.CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified
                     && entry.Metadata.FindProperty(UpdatedAt) is not null)
            {
                entry.Property(UpdatedAt).CurrentValue = now;
            }

            if (entry.Entity is ISoftDeletable { IsDeleted: true, DeletedAt: null } softDeletable)
                softDeletable.DeletedAt = now;
        }
    }
}
