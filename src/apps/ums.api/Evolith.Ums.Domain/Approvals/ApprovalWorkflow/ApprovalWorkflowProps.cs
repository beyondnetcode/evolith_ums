namespace Evolith.Ums.Domain.Approvals.ApprovalWorkflow;

public class ApprovalWorkflowProps : IProps
{
    public IdValueObject Id { get; set; }
    public TenantId TenantId { get; set; }
    public SystemSuiteId? SystemSuiteId { get; set; }
    public Code Code { get; set; }
    public Name Name { get; set; }
    public Description Description { get; set; }
    public UserCategory TargetUserCategory { get; set; }
    public bool RequiresApproval { get; set; }
    public AuditValueObject Audit { get; private set; }

    public ApprovalWorkflowProps(
        IdValueObject id,
        TenantId tenantId,
        SystemSuiteId? systemSuiteId,
        Code code,
        Name name,
        Description description,
        UserCategory targetUserCategory,
        bool requiresApproval,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        SystemSuiteId = systemSuiteId;
        Code = code;
        Name = name;
        Description = description;
        TargetUserCategory = targetUserCategory;
        RequiresApproval = requiresApproval;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public object Clone() => MemberwiseClone();
}
