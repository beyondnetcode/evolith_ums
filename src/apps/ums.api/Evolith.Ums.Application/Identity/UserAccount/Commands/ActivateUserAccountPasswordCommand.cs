namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record ActivateUserAccountPasswordCommand(Guid UserAccountId, Guid CredentialId) : ICommand;
