#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Configuration.IdpConfiguration.Events;

using BeyondNetCode.Shell.Ddd.Services.Impl;

public class IdpConfigurationDomainEventsManager : DomainEventsManager
{
    public IdpConfigurationDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(IdpConfigRegisteredEvent @event) { }
    private void Apply(IdpConfigActivatedEvent @event) { }
    private void Apply(IdpConfigUpdatedEvent @event) { }
    private void Apply(IdpConfigDeactivatedEvent @event) { }
}

#pragma warning restore S1144, S1186
