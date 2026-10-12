namespace Evolith.Ums.Domain.Authorization.Role;

using Evolith.Ums.Domain.Events;

public sealed class Role : AggregateRoot<Role, RoleProps>
{
    public new RoleDomainEventsManager DomainEvents { get; }

    private Role(RoleProps props) : base(props)
    {
        DomainEvents = new RoleDomainEventsManager(this);

        if (Props == null || TrackingState == null || Props.Id == null) return;
        if (!TrackingState.IsNew) return;

        try
        {
            DomainEvents.RaiseEvent(new RoleCreatedEvent(
                Props.Id.GetValue(),
                Props.TenantId.GetValue(),
                Props.SystemSuiteId.GetValue(),
                Props.Code.GetValue(),
                Props.ParentRoleId?.GetValue()));
        }
        catch
        {
            // Suppress domain event creation errors during construction
        }
    }

    public TenantId TenantId => Props.TenantId;
    public SystemSuiteId SystemSuiteId => Props.SystemSuiteId;
    public RoleId? ParentRoleId => Props.ParentRoleId;
    public Code Code => Props.Code;
    public Name Value => Props.Value;
    public Description Description => Props.Description;
    public int HierarchyLevel => Props.HierarchyLevel;
    public int PromotionOrder => Props.PromotionOrder;
    public bool IsActive => Props.IsActive;

    public RoleId GetId() => RoleId.Load(Props.Id.GetValue());

    public static Result<Role> Create(
        TenantId tenantId,
        SystemSuiteId systemSuiteId,
        Code code,
        Name value,
        Description description,
        RoleId? parentRoleId,
        int hierarchyLevel,
        int promotionOrder,
        ActorId createdBy)
    {
        var props = new RoleProps(
            IdValueObject.Create(),
            tenantId,
            systemSuiteId,
            parentRoleId,
            code,
            value,
            description,
            hierarchyLevel,
            promotionOrder,
            createdBy);
        var role = new Role(props);
        role.ValidateHierarchy();

        return role.IsValid()
            ? Result<Role>.Success(role)
            : Result<Role>.Failure(role.BrokenRules.GetBrokenRulesAsString());
    }

    public Result Update(
        Name value,
        Description description,
        RoleId? parentRoleId,
        int hierarchyLevel,
        int promotionOrder,
        ActorId updatedBy)
    {
        SetProps(Props.WithValue(value).WithDescription(description).WithParentRoleId(parentRoleId).WithHierarchyLevel(hierarchyLevel).WithPromotionOrder(promotionOrder));
        ValidateHierarchy();

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        DomainEvents.RaiseEvent(new RoleUpdatedEvent(Props.Id.GetValue()));
        return Result.Success();
    }

    public Result Activate(ActorId updatedBy)
    {
        if (IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Authorization.RoleAlreadyActive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithIsActive(true));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        DomainEvents.RaiseEvent(new RoleActivatedEvent(Props.Id.GetValue()));
        return Result.Success();
    }

    public Result Deactivate(ActorId updatedBy, int activeProfileCount = 0, int activeChildRoleCount = 0)
    {
        if (!IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Authorization.RoleAlreadyInactive));
        }

        if (activeProfileCount > 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Authorization.RoleHasActiveProfiles));
        }

        if (activeChildRoleCount > 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Authorization.RoleHasActiveChildRoles));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithIsActive(false));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        DomainEvents.RaiseEvent(new RoleDeactivatedEvent(Props.Id.GetValue()));
        return Result.Success();
    }

    private void ValidateHierarchy()
    {
        if (Props.HierarchyLevel < 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(HierarchyLevel), DomainErrors.Authorization.RoleHierarchyLevelInvalid));
        }

        if (Props.PromotionOrder < 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(PromotionOrder), DomainErrors.Authorization.RolePromotionOrderInvalid));
        }

        if (Props.ParentRoleId is null && Props.HierarchyLevel != 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(HierarchyLevel), DomainErrors.Authorization.RootRoleHierarchyLevelInvalid));
        }

        if (Props.ParentRoleId is not null && Props.HierarchyLevel == 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(HierarchyLevel), DomainErrors.Authorization.ChildRoleHierarchyLevelInvalid));
        }
    }
}
