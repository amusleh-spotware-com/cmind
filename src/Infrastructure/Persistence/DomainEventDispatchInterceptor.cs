using Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence;

public sealed class DomainEventDispatchInterceptor(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
        if (eventData.Context is not null) await DispatchAsync(eventData.Context, ct);
        return await base.SavedChangesAsync(eventData, result, ct);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null) ClearDomainEvents(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    private async Task DispatchAsync(DbContext context, CancellationToken ct)
    {
        var entities = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();
        if (entities.Count == 0) return;
        var events = entities.SelectMany(e => e.DomainEvents).ToList();
        var now = timeProvider.GetUtcNow();
        foreach (var domainEvent in events)
            (domainEvent as DomainEventBase)?.StampOccurredAt(now);
        foreach (var entity in entities) entity.ClearDomainEvents();
        using var scope = scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        await dispatcher.DispatchAsync(events, ct);
    }

    private static void ClearDomainEvents(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<IHasDomainEvents>())
            entry.Entity.ClearDomainEvents();
    }
}
