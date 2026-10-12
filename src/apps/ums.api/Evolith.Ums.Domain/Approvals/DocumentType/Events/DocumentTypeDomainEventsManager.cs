#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Approvals.DocumentType.Events;

public class DocumentTypeDomainEventsManager : DomainEventsManager
{
    public DocumentTypeDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(DocumentTypeRegisteredEvent @event) { }
    private void Apply(DocumentTypeUpdatedEvent @event) { }
}

#pragma warning restore S1144, S1186
