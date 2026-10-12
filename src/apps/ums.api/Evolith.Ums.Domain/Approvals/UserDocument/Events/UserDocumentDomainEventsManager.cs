#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Approvals.UserDocument.Events;

public class UserDocumentDomainEventsManager : DomainEventsManager
{
    public UserDocumentDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(DocumentUploadedEvent @event) { }
    private void Apply(DocumentValidatedEvent @event) { }
    private void Apply(DocumentRejectedEvent @event) { }
    private void Apply(DocumentExpiredEvent @event) { }
    private void Apply(DocumentNearExpirationEvent @event) { }
    private void Apply(EnforcementExecutedEvent @event) { }
}

#pragma warning restore S1144, S1186
