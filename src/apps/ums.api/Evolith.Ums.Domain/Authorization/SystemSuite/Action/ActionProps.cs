namespace Evolith.Ums.Domain.Authorization.SystemSuite.Action;

public class ActionProps : IProps
{
    public IdValueObject Id { get; set; }
    public TenantId TenantId { get; set; }
    public SystemSuiteId? SystemSuiteId { get; set; }
    public ModuleId? ModuleId { get; set; }
    public ActionCode Code { get; set; }
    public Name Name { get; set; }
    public AuditValueObject Audit { get; private set; }

    public ActionProps(
        IdValueObject id,
        TenantId tenantId,
        SystemSuiteId? systemSuiteId,
        ModuleId? moduleId,
        ActionCode code,
        Name name,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        SystemSuiteId = systemSuiteId;
        ModuleId = moduleId;
        Code = code;
        Name = name;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public object Clone() => MemberwiseClone();
}
