namespace Evolith.Ums.Application.Authorization.Template.Commands;

public sealed record DeletePermissionTemplateCommand(Guid TemplateId) : ICommand;
