namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record ModifyUserValidityPeriodCommand(
    Guid UserAccountId,
    DateTimeOffset ExpiresAt,
    string? Reason = null) : ICommand;
