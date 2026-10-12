namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record DenyUserSignupCommand(
    Guid UserAccountId,
    string? Reason = null) : ICommand;
