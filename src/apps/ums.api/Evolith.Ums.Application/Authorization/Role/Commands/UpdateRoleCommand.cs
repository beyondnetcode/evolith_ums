namespace Evolith.Ums.Application.Authorization.Role.Commands;

public sealed record UpdateRoleCommand(
    Guid RoleId,
    string Value,
    string Description,
    Guid? ParentRoleId,
    int HierarchyLevel,
    int PromotionOrder) : ICommand;
