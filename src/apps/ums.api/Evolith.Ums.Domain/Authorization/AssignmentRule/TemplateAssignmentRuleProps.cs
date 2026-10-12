namespace Evolith.Ums.Domain.Authorization.AssignmentRule;

public sealed class TemplateAssignmentRuleProps : IProps
{
    public IdValueObject Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public TemplateId TemplateId { get; private set; }
    public RoleId RoleId { get; private set; }
    public int Priority { get; private set; }
    public TemplateAssignmentRuleStatus Status { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public TemplateAssignmentRuleProps(
        IdValueObject id,
        TenantId tenantId,
        TemplateId templateId,
        RoleId roleId,
        int priority,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        TemplateId = templateId;
        RoleId = roleId;
        Priority = priority;
        Status = TemplateAssignmentRuleStatus.Active;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public TemplateAssignmentRuleProps(
        IdValueObject id,
        TenantId tenantId,
        TemplateId templateId,
        RoleId roleId,
        int priority,
        TemplateAssignmentRuleStatus status,
        AuditValueObject audit)
    {
        Id = id;
        TenantId = tenantId;
        TemplateId = templateId;
        RoleId = roleId;
        Priority = priority;
        Status = status;
        Audit = audit;
    }

    public TemplateAssignmentRuleProps WithStatus(TemplateAssignmentRuleStatus status)
    {
        var clone = (TemplateAssignmentRuleProps)MemberwiseClone();
        clone.Status = status;
        return clone;
    }

    public TemplateAssignmentRuleProps WithPriority(int priority)
    {
        var clone = (TemplateAssignmentRuleProps)MemberwiseClone();
        clone.Priority = priority;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}
