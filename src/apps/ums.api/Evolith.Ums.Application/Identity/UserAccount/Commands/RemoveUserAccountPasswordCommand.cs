namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record RemoveUserAccountPasswordCommand(Guid UserAccountId, Guid CredentialId) : ICommand;
