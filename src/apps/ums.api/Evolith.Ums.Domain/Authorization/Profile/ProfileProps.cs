namespace Evolith.Ums.Domain.Authorization.Profile;

public class ProfileProps : IProps
{
    public IdValueObject Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public UserId UserId { get; private set; }
    public RoleId RoleId { get; private set; }
    public BranchId? BranchId { get; private set; }
    public ProfileScope Scope { get; private set; }
    public bool IsActive { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public ProfileProps(
        IdValueObject id,
        TenantId tenantId,
        UserId userId,
        RoleId roleId,
        BranchId? branchId,
        ProfileScope scope,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        RoleId = roleId;
        BranchId = branchId;
        Scope = scope;
        IsActive = true;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public ProfileProps(
        IdValueObject id,
        TenantId tenantId,
        UserId userId,
        RoleId roleId,
        BranchId? branchId,
        ProfileScope scope,
        bool isActive,
        AuditValueObject audit)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        RoleId = roleId;
        BranchId = branchId;
        Scope = scope;
        IsActive = isActive;
        Audit = audit;
    }

    public ProfileProps WithIsActive(bool isActive)
    {
        var clone = (ProfileProps)MemberwiseClone();
        clone.IsActive = isActive;
        return clone;
    }

    /// <summary>ADR-UMS-096: reasigna el rol del perfil (efecto de la promoción de rol IGA).</summary>
    public ProfileProps WithRoleId(RoleId roleId)
    {
        var clone = (ProfileProps)MemberwiseClone();
        clone.RoleId = roleId;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}
