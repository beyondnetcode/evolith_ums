namespace Evolith.Ums.Application.Authorization.Template.Commands;

using Evolith.Ums.Application.Authorization.Template.DTOs;

public sealed record AddTemplateItemCommand(
    Guid TemplateId,
    string TargetType,
    Guid TargetId,
    Guid ActionId,
    bool IsAllowed,
    bool IsDenied) : ICommand<AddTemplateItemResponse>;
