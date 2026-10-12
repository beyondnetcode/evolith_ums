namespace Evolith.Ums.Application.Authorization.Profile.Commands;

public sealed record AssignProfileTemplateCommand(Guid ProfileId, Guid TemplateId) : ICommand;
