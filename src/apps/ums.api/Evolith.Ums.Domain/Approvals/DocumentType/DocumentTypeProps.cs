namespace Evolith.Ums.Domain.Approvals.DocumentType;

public class DocumentTypeProps : IProps
{
    public IdValueObject Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Code Code { get; private set; }
    public Name Name { get; private set; }
    public Description Description { get; private set; }
    public DocumentCriticity Criticity { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public DocumentTypeProps(
        IdValueObject id,
        TenantId tenantId,
        Code code,
        Name name,
        Description description,
        DocumentCriticity criticity,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        Name = name;
        Description = description;
        Criticity = criticity;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public DocumentTypeProps WithName(Name name)
    {
        var clone = (DocumentTypeProps)MemberwiseClone();
        clone.Name = name;
        return clone;
    }

    public DocumentTypeProps WithDescription(Description description)
    {
        var clone = (DocumentTypeProps)MemberwiseClone();
        clone.Description = description;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}