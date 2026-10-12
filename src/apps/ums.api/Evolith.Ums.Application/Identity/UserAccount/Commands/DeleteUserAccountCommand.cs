namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

/// <summary>
/// REC-16: Soft-delete a user account by ID.
/// The infrastructure layer handles GDPR anonymization (email + IdentityReference).
/// </summary>
public sealed record DeleteUserAccountCommand(Guid UserAccountId) : ICommand;
