using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Identity.Auth;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

namespace Evolith.Ums.Application.Identity.Auth;

/// <summary>
/// Validates user credentials against the internal BCrypt password store.
/// Extracted from AuthEndpoints.HandleLoginAsync so the logic is testable and reusable.
/// </summary>
public sealed class LocalAuthStrategyService : ILocalAuthStrategy
{
    private readonly IPasswordHashingService _hasher;

    public LocalAuthStrategyService(IPasswordHashingService hasher)
    {
        _hasher = hasher;
    }

    public Result Authenticate(UserAccountAggregate user, string plainTextPassword)
    {
        var activeCredential = user.PasswordCredentials.FirstOrDefault(c => c.Props.IsActive);

        if (activeCredential is null)
            return Result.Failure("AUTH_006: User has no active password credential.");

        if (!_hasher.Verify(plainTextPassword, activeCredential.Props.PasswordHash.GetValue()))
            return Result.Failure("AUTH_006: Invalid username or password.");

        return Result.Success();
    }
}
