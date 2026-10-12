#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Configuration.AppConfiguration.Events;

using BeyondNetCode.Shell.Ddd.Services.Impl;

public class AppConfigurationDomainEventsManager : DomainEventsManager
{
    public AppConfigurationDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(AppConfigCreatedEvent @event) { }
    private void Apply(AppConfigPublishedEvent @event) { }
    private void Apply(AppConfigArchivedEvent @event) { }
    private void Apply(AppConfigDeletedEvent @event) { }
    private void Apply(AppConfigUpdatedEvent @event) { }
}

#pragma warning restore S1144, S1186
