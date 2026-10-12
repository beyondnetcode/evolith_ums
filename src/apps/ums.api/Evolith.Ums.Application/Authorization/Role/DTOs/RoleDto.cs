namespace Evolith.Ums.Application.Authorization.Role.DTOs;

public sealed record RoleDto(
    Guid RoleId,
    Guid TenantId,
    Guid SystemSuiteId,
    Guid? ParentRoleId,
    string Code,
    string Value,
    string Description,
    int HierarchyLevel,
    int PromotionOrder,
    bool IsActive)
{
    public static RoleDto Map(Evolith.Ums.Domain.Authorization.Role.Role role) => new(
        role.GetId().GetValue(),
        role.TenantId.GetValue(),
        role.SystemSuiteId.GetValue(),
        role.ParentRoleId?.GetValue(),
        role.Code.GetValue(),
        role.Value.GetValue(),
        role.Description.GetValue(),
        role.HierarchyLevel,
        role.PromotionOrder,
        role.IsActive);
}
