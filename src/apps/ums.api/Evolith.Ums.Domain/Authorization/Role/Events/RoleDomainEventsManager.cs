#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Authorization.Role;

public sealed class RoleDomainEventsManager : DomainEventsManager
{
    public RoleDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(RoleCreatedEvent @event) { }
    private void Apply(RoleUpdatedEvent @event) { }
    private void Apply(RoleActivatedEvent @event) { }
    private void Apply(RoleDeactivatedEvent @event) { }
}

#pragma warning restore S1144, S1186
