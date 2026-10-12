#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Identity.Tenant.Events;

public class TenantDomainEventsManager : DomainEventsManager
{
    public TenantDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(TenantCreatedEvent @event) { }
    private void Apply(TenantUpdatedEvent @event) { }
    private void Apply(TenantSuspendedEvent @event) { }
    private void Apply(TenantActivatedEvent @event) { }
    private void Apply(TenantArchivedEvent @event) { }
    private void Apply(BranchCreatedEvent @event) { }
    private void Apply(BranchUpdatedEvent @event) { }
    private void Apply(BranchClosedEvent @event) { }
    private void Apply(BranchDeactivatedEvent @event) { }
    private void Apply(BranchReactivatedEvent @event) { }
    private void Apply(IdentityProviderRegisteredEvent @event) { }
    private void Apply(IdentityProviderActivatedEvent @event) { }
    private void Apply(IdentityProviderDeactivatedEvent @event) { }
    private void Apply(IdentityProviderRemovedEvent @event) { }
}

#pragma warning restore S1144, S1186
