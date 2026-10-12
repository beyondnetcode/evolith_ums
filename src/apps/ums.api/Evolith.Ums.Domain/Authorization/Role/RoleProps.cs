namespace Evolith.Ums.Domain.Authorization.Role;

public sealed class RoleProps : IProps
{
    public IdValueObject Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public SystemSuiteId SystemSuiteId { get; private set; }
    public RoleId? ParentRoleId { get; private set; }
    public Code Code { get; private set; }
    public Name Value { get; private set; }
    public Description Description { get; private set; }
    public int HierarchyLevel { get; private set; }
    public int PromotionOrder { get; private set; }
    public bool IsActive { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public RoleProps(
        IdValueObject id,
        TenantId tenantId,
        SystemSuiteId systemSuiteId,
        RoleId? parentRoleId,
        Code code,
        Name value,
        Description description,
        int hierarchyLevel,
        int promotionOrder,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        SystemSuiteId = systemSuiteId;
        ParentRoleId = parentRoleId;
        Code = code;
        Value = value;
        Description = description;
        HierarchyLevel = hierarchyLevel;
        PromotionOrder = promotionOrder;
        IsActive = true;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public RoleProps(
        IdValueObject id,
        TenantId tenantId,
        SystemSuiteId systemSuiteId,
        RoleId? parentRoleId,
        Code code,
        Name value,
        Description description,
        int hierarchyLevel,
        int promotionOrder,
        bool isActive,
        AuditValueObject audit)
    {
        Id = id;
        TenantId = tenantId;
        SystemSuiteId = systemSuiteId;
        ParentRoleId = parentRoleId;
        Code = code;
        Value = value;
        Description = description;
        HierarchyLevel = hierarchyLevel;
        PromotionOrder = promotionOrder;
        IsActive = isActive;
        Audit = audit;
    }

    public RoleProps WithValue(Name value)
    {
        var clone = (RoleProps)MemberwiseClone();
        clone.Value = value;
        return clone;
    }

    public RoleProps WithDescription(Description description)
    {
        var clone = (RoleProps)MemberwiseClone();
        clone.Description = description;
        return clone;
    }

    public RoleProps WithParentRoleId(RoleId? parentRoleId)
    {
        var clone = (RoleProps)MemberwiseClone();
        clone.ParentRoleId = parentRoleId;
        return clone;
    }

    public RoleProps WithHierarchyLevel(int hierarchyLevel)
    {
        var clone = (RoleProps)MemberwiseClone();
        clone.HierarchyLevel = hierarchyLevel;
        return clone;
    }

    public RoleProps WithPromotionOrder(int promotionOrder)
    {
        var clone = (RoleProps)MemberwiseClone();
        clone.PromotionOrder = promotionOrder;
        return clone;
    }

    public RoleProps WithIsActive(bool isActive)
    {
        var clone = (RoleProps)MemberwiseClone();
        clone.IsActive = isActive;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}