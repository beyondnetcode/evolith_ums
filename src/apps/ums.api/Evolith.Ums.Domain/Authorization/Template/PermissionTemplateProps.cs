namespace Evolith.Ums.Domain.Authorization.Template;

public class PermissionTemplateProps : IProps
{
    public IdValueObject Id { get; set; }
    public TenantId TenantId { get; set; }
    public RoleId RoleId { get; set; }
    public SystemSuiteId SystemSuiteId { get; set; }
    public TemplateVersion Version { get; set; }
    public TemplateStatus Status { get; set; }
    public AuditValueObject Audit { get; private set; }

    public PermissionTemplateProps(
        IdValueObject id,
        TenantId tenantId,
        RoleId roleId,
        SystemSuiteId systemSuiteId,
        TemplateVersion version,
        TemplateStatus status,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        RoleId = roleId;
        SystemSuiteId = systemSuiteId;
        Version = version;
        Status = status;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public object Clone() => MemberwiseClone();
}
