namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record RemovePasswordCommand(Guid UserAccountId, Guid CredentialId) : ICommand;
