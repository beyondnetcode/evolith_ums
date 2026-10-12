namespace Evolith.Ums.Domain.Authorization.SystemSuite.DomainResource;

public class DomainResourceProps : IProps
{
    public IdValueObject Id { get; private set; }
    public SystemSuiteId SystemSuiteId { get; private set; }
    public ModuleId? ModuleId { get; private set; }
    /// <summary>Id of the parent Aggregate when this resource is a child Entity or DomainMethod.</summary>
    public IdValueObject? ParentResourceId { get; private set; }
    public DomainResourceType Type { get; private set; }
    public Code Code { get; private set; }
    public Name Name { get; private set; }
    public Description Description { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public DomainResourceProps(
        IdValueObject id,
        SystemSuiteId systemSuiteId,
        ModuleId? moduleId,
        IdValueObject? parentResourceId,
        DomainResourceType type,
        Code code,
        Name name,
        Description description,
        ActorId createdBy)
    {
        Id = id;
        SystemSuiteId = systemSuiteId;
        ModuleId = moduleId;
        ParentResourceId = parentResourceId;
        Type = type;
        Code = code;
        Name = name;
        Description = description;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public DomainResourceProps WithName(Name name)
    {
        var clone = (DomainResourceProps)MemberwiseClone();
        clone.Name = name;
        return clone;
    }

    public DomainResourceProps WithDescription(Description description)
    {
        var clone = (DomainResourceProps)MemberwiseClone();
        clone.Description = description;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}