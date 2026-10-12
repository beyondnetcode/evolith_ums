namespace Evolith.Ums.Application.Authorization.Template.Commands;

public sealed record DeactivateTemplateItemCommand(Guid TemplateId, Guid ItemId) : ICommand;
