namespace Core.Domain;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

public interface IAggregateRoot;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct);
}

public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}

public sealed class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code)
        : base(code) => Code = code;

    public DomainException(string code, string message)
        : base(message) => Code = code;
}

public static class DomainGuard
{
    public static string AgainstNullOrWhiteSpace(string? value, string code)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainException(code);
        return value.Trim();
    }

    public static void AgainstNegative(double value, string code)
    {
        if (value < 0) throw new DomainException(code);
    }

    public static void AgainstOutOfInclusiveRange(int value, int min, int max, string code)
    {
        if (value < min || value > max) throw new DomainException(code);
    }
}
