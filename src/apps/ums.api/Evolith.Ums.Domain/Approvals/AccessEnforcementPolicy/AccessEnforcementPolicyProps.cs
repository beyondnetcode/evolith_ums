namespace Evolith.Ums.Domain.Approvals.AccessEnforcementPolicy;

public class AccessEnforcementPolicyProps : IProps
{
    public IdValueObject Id { get; set; }
    public TenantId TenantId { get; set; }
    public ProfileId? ProfileId { get; set; }
    public RoleId? RoleId { get; set; }
    public AccessEnforcementAction EnforcementAction { get; set; }
    public bool IsActive { get; set; }
    // G-120 (FR-053): periodo de gracia en días antes de que la acción de enforcement se aplique a un
    // documento crítico vencido/faltante. 0 = enforcement inmediato (comportamiento previo).
    public int GracePeriodDays { get; set; }
    public AuditValueObject Audit { get; private set; }

    public AccessEnforcementPolicyProps(
        IdValueObject id,
        TenantId tenantId,
        ProfileId? profileId,
        RoleId? roleId,
        AccessEnforcementAction enforcementAction,
        bool isActive,
        int gracePeriodDays,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        ProfileId = profileId;
        RoleId = roleId;
        EnforcementAction = enforcementAction;
        IsActive = isActive;
        GracePeriodDays = gracePeriodDays;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public object Clone() => MemberwiseClone();
}
