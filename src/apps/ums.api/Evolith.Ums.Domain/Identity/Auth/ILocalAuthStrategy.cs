namespace Evolith.Ums.Domain.Identity.Auth;

using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

/// <summary>
/// Validates credentials against the user's internal BCrypt password store.
/// Extracted from AuthEndpoints so the logic is testable and reusable.
/// </summary>
public interface ILocalAuthStrategy
{
    /// <summary>
    /// Returns success when the password matches the user's active credential.
    /// Returns failure with AUTH_006 when credentials are invalid or the
    /// user has no active credential record.
    /// </summary>
    Result Authenticate(UserAccountAggregate user, string plainTextPassword);
}
