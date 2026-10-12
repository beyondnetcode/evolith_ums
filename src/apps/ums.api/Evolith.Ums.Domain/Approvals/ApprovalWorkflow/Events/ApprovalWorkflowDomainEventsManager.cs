#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Approvals.ApprovalWorkflow.Events;

public class ApprovalWorkflowDomainEventsManager : DomainEventsManager
{
    public ApprovalWorkflowDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(ApprovalWorkflowCreatedEvent @event) { }
    private void Apply(ApprovalWorkflowDocumentAddedEvent @event) { }
    private void Apply(ApprovalWorkflowDocumentRemovedEvent @event) { }
    private void Apply(ApprovalWorkflowActivatedEvent @event) { }
    private void Apply(ApprovalWorkflowDeactivatedEvent @event) { }
}
#pragma warning restore S1144, S1186
