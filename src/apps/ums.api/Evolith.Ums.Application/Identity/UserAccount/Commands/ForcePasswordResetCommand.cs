namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record ForcePasswordResetCommand(Guid UserAccountId) : ICommand<ForcePasswordResetResponse>;

public sealed record ForcePasswordResetResponse(string TemporaryPassword);
