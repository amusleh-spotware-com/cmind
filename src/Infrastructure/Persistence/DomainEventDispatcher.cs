using Core.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence;

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
            foreach (var handler in serviceProvider.GetServices(handlerType))
            {
                if (handler is null) continue;
                await (Task)method.Invoke(handler, [domainEvent, ct])!;
            }
        }
    }
}
