#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Authorization.Profile;

public class ProfileDomainEventsManager : DomainEventsManager
{
    public ProfileDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(ProfileCreatedEvent @event) { }
    private void Apply(TemplateAutoAssignedEvent @event) { }
    private void Apply(TemplateLinkedToProfileEvent @event) { }

    private void Apply(TemplateUnlinkedFromProfileEvent @event) { }
    private void Apply(PermissionOverriddenEvent @event) { }
    private void Apply(PermissionStatusChangedEvent @event) { }
    private void Apply(ProfileDeactivatedEvent @event) { }
    private void Apply(ProfileActivatedEvent @event) { }
    private void Apply(ProfileRoleChangedEvent @event) { }
}

#pragma warning restore S1144, S1186
