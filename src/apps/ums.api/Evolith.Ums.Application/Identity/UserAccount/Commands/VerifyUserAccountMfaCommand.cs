namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record VerifyUserAccountMfaCommand(Guid UserAccountId, Guid EnrollmentId) : ICommand;
