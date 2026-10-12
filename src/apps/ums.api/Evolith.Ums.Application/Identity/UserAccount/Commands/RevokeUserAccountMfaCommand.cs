namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record RevokeUserAccountMfaCommand(Guid UserAccountId, Guid EnrollmentId) : ICommand;
