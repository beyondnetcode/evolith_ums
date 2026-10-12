namespace Evolith.Ums.Application.Authorization.Template.DTOs;

public sealed record PermissionTemplateItemDto(
    Guid ItemId,
    string TargetType,
    Guid TargetId,
    string TargetName,
    Guid ActionId,
    string ActionName,
    bool IsAllowed,
    bool IsDenied,
    bool IsActive);
