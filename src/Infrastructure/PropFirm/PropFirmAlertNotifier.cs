using Core.Domain;
using Core.Logging;
using Microsoft.Extensions.Logging;

namespace Infrastructure.PropFirm;

/// <summary>
/// Reacts to prop-firm challenge domain events after a successful save and notifies the user that a
/// challenge passed, failed, or is approaching a drawdown limit. Delivery is the structured alert/audit
/// trail (<see cref="LogMessages"/>); the live UI reflects the same status change. This is a cross-context
/// reaction wired as an event handler — it never mutates the challenge aggregate.
/// </summary>
public sealed class PropFirmAlertNotifier(ILogger<PropFirmAlertNotifier> log)
    : IDomainEventHandler<PropFirmChallengePassed>,
        IDomainEventHandler<PropFirmChallengeBreached>,
        IDomainEventHandler<PropFirmDrawdownWarning>
{
    public Task HandleAsync(PropFirmChallengePassed domainEvent, CancellationToken ct)
    {
        log.PropFirmAlertPassed(domainEvent.ChallengeId.Value, domainEvent.UserId.Value);
        return Task.CompletedTask;
    }

    public Task HandleAsync(PropFirmChallengeBreached domainEvent, CancellationToken ct)
    {
        log.PropFirmAlertBreached(domainEvent.ChallengeId.Value, domainEvent.Reason.ToString(), domainEvent.UserId.Value);
        return Task.CompletedTask;
    }

    public Task HandleAsync(PropFirmDrawdownWarning domainEvent, CancellationToken ct)
    {
        log.PropFirmAlertDrawdownWarning(domainEvent.ChallengeId.Value, domainEvent.PercentUsed, domainEvent.UserId.Value);
        return Task.CompletedTask;
    }
}
