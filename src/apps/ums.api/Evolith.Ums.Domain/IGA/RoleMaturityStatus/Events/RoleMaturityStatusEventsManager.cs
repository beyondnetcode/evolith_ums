#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.IGA.RoleMaturityStatus.Events;

using Evolith.Ums.Domain.Events;

public class RoleMaturityStatusEventsManager : DomainEventsManager
{
    public RoleMaturityStatusEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(RoleMaturityStatusCreatedEvent @event) { }
    private void Apply(RoleMaturityLevelChangedEvent @event) { }
    private void Apply(RoleMaturityPerformanceScoreUpdatedEvent @event) { }
    private void Apply(RoleMaturityComplianceIssueMarkedEvent @event) { }
    private void Apply(RoleMaturityComplianceIssueResolvedEvent @event) { }
    private void Apply(RoleMaturityEligibilityConfirmedEvent @event) { }
}

#pragma warning restore S1144, S1186
