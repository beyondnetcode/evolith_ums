#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Authorization.Template;

public class PermissionTemplateDomainEventsManager : DomainEventsManager
{
    public PermissionTemplateDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(PermissionTemplateCreatedEvent @event) { }
    private void Apply(PermissionTemplatePublishedEvent @event) { }
    private void Apply(PermissionTemplateMutatedEvent @event) { }
    private void Apply(PermissionTemplateDeprecatedEvent @event) { }
    private void Apply(PermissionTemplateDeletedEvent @event) { }
}

#pragma warning restore S1144, S1186
