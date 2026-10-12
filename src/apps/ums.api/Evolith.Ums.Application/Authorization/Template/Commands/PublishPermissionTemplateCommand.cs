using Evolith.Ums.Application.Authorization.Template.DTOs;

namespace Evolith.Ums.Application.Authorization.Template.Commands;

public sealed record PublishPermissionTemplateCommand(Guid TemplateId) : ICommand;
