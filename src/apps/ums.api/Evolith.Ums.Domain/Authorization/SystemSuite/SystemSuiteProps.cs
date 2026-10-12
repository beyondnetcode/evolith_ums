namespace Evolith.Ums.Domain.Authorization.SystemSuite;

public class SystemSuiteProps : IProps
{
    public IdValueObject Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Code Code { get; private set; }
    public Name Name { get; private set; }
    public Description Description { get; private set; }
    public SystemStatus Status { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public SystemSuiteProps(
        IdValueObject id,
        TenantId tenantId,
        Code code,
        Name name,
        Description description,
        SystemStatus status,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        Name = name;
        Description = description;
        Status = status;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public SystemSuiteProps WithName(Name name)
    {
        var clone = (SystemSuiteProps)MemberwiseClone();
        clone.Name = name;
        return clone;
    }

    public SystemSuiteProps WithDescription(Description description)
    {
        var clone = (SystemSuiteProps)MemberwiseClone();
        clone.Description = description;
        return clone;
    }

    public SystemSuiteProps WithStatus(SystemStatus status)
    {
        var clone = (SystemSuiteProps)MemberwiseClone();
        clone.Status = status;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}
