namespace Evolith.Ums.Domain.Approvals.DocumentType;

using Evolith.Ums.Domain.Approvals.DocumentType.Events;

public sealed class DocumentType : AggregateRoot<DocumentType, DocumentTypeProps>
{
    public new DocumentTypeDomainEventsManager DomainEvents { get; }

    private DocumentType(DocumentTypeProps props) : base(props)
    {
        DomainEvents = new DocumentTypeDomainEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new DocumentTypeRegisteredEvent(
                props.Id.GetValue(), props.Criticity.Name, props.TenantId.GetValue()));
        }
    }

    public TenantId TenantId => Props.TenantId;
    public Code Code => Props.Code;
    public Name Name => Props.Name;
    public Description Description => Props.Description;
    public DocumentCriticity Criticity => Props.Criticity;

    public DocumentTypeId GetId() => DocumentTypeId.Load(Props.Id.GetValue());

    public static Result<DocumentType> Create(
        TenantId tenantId,
        Code code,
        Name name,
        Description description,
        DocumentCriticity criticity,
        ActorId createdBy)
    {
        var props = new DocumentTypeProps(IdValueObject.Create(), tenantId, code, name, description, criticity, createdBy);
        var documentType = new DocumentType(props);

        if (!documentType.IsValid())
        {
            return Result<DocumentType>.Failure(documentType.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<DocumentType>.Success(documentType);
    }

    public Result Update(Name name, Description description, ActorId updatedBy)
    {
        var oldName = Props.Name.GetValue();
        SetProps(Props.WithName(name).WithDescription(description));

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        DomainEvents.RaiseEvent(new DocumentTypeUpdatedEvent(
            Props.Id.GetValue(),
            oldName,
            name.GetValue(),
            updatedBy.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }
}
