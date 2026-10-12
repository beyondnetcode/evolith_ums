#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Authorization.SystemSuite.Events;

public class SystemSuiteDomainEventsManager : DomainEventsManager
{
    public SystemSuiteDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(SystemSuiteRegisteredEvent @event) { }
    private void Apply(SystemSuiteStatusChangedEvent @event) { }
    private void Apply(SystemSuiteDeletedEvent @event) { }
    private void Apply(SystemSuiteModuleAddedEvent @event) { }
    private void Apply(SystemSuiteModuleRemovedEvent @event) { }
    private void Apply(SystemSuiteModuleStatusChangedEvent @event) { }
    private void Apply(SystemSuiteActionRegisteredEvent @event) { }
    private void Apply(SystemSuiteActionRemovedEvent @event) { }
}

#pragma warning restore S1144, S1186
