#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Identity.UserManagementDelegation;

using Evolith.Ums.Domain.Events;

public class UserManagementDelegationEventsManager : DomainEventsManager
{
    public UserManagementDelegationEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(DelegationCreatedEvent @event) { }
    private void Apply(DelegationActivatedEvent @event) { }
    private void Apply(DelegationRevokedEvent @event) { }
    private void Apply(DelegationExpiredEvent @event) { }
    private void Apply(DelegationRejectedEvent @event) { }
    private void Apply(DelegationArchivedEvent @event) { }
}

#pragma warning restore S1144, S1186
