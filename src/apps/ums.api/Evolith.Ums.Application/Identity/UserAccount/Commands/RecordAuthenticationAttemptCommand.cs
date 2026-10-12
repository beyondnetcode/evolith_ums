namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record RecordAuthenticationAttemptCommand(
    Guid UserAccountId,
    bool Success,
    string Reason,
    string IpAddress) : ICommand;
