#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Approvals.ApprovalRequest.Events;

public class ApprovalRequestDomainEventsManager : DomainEventsManager
{
    public ApprovalRequestDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(ApprovalRequestApprovedEvent @event) { }
    private void Apply(ApprovalRequestRejectedEvent @event) { }
    private void Apply(ProfileAssignedToUserEvent @event) { }
    private void Apply(ApprovalRequestCancelledEvent @event) { }
}

#pragma warning restore S1144, S1186
