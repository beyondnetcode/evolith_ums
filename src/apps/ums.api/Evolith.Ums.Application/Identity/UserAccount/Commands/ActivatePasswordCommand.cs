namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record ActivatePasswordCommand(Guid UserAccountId, Guid CredentialId) : ICommand;
