namespace Evolith.Ums.Application.Authorization.Profile.Commands;

public sealed record AssignTemplateToProfileCommand(Guid ProfileId, Guid TemplateId) : ICommand;
