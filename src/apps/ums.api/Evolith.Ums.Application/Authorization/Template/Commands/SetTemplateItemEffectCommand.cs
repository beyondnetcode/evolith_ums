namespace Evolith.Ums.Application.Authorization.Template.Commands;

/// <summary>Effect: "Allow" | "Deny" | "Neutral"</summary>
public sealed record SetTemplateItemEffectCommand(Guid TemplateId, Guid ItemId, string Effect) : ICommand;
