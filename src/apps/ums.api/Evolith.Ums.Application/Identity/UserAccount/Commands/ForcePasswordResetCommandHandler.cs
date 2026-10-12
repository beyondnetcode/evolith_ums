using System.Security.Cryptography;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed class ForcePasswordResetCommandHandler : ICommandHandler<ForcePasswordResetCommand, ForcePasswordResetResponse>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly INotificationService _notificationService;
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUserManagementDelegationAccessService _delegationAccessService;

    public ForcePasswordResetCommandHandler(
        IUserAccountRepository userAccountRepository,
        IUserContext userContext,
        IPasswordHashingService passwordHashingService,
        INotificationService notificationService,
        ITenantScopePolicy tenantScopePolicy,
        IUserManagementDelegationAccessService delegationAccessService)
    {
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
        _passwordHashingService = passwordHashingService;
        _notificationService = notificationService;
        _tenantScopePolicy = tenantScopePolicy;
        _delegationAccessService = delegationAccessService;
    }

    [AuthorizationAspect(ResourceCode = "manage_users", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<ForcePasswordResetResponse>> Handle(ForcePasswordResetCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result<ForcePasswordResetResponse>.Failure("Authenticated user is required.");

        var userAccount = await _userAccountRepository.GetByIdAsync(request.UserAccountId, cancellationToken);
        if (userAccount is null)
            return Result<ForcePasswordResetResponse>.Failure("User account not found.");

        var ownerScopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(userAccount.TenantId.GetValue(), cancellationToken);
        if (ownerScopeResult.IsFailure)
        {
            var delegatedAccess = await _delegationAccessService.EnsureCanExecuteAsync(
                userAccount.TenantId.GetValue(),
                Evolith.Ums.Domain.Identity.UserManagementDelegation.DelegatedAction.ResetPassword,
                null,
                cancellationToken);

            if (delegatedAccess.IsFailure)
            {
                return Result<ForcePasswordResetResponse>.Failure(ownerScopeResult.Error);
            }
        }

        if (userAccount.IdentityReference is not null)
            return Result<ForcePasswordResetResponse>.Failure(
                "USER_015: Force reset is not available for federated accounts. Credentials are managed by the external identity provider.");

        var tempPassword = GenerateTemporaryPassword();
        var passwordHash = _passwordHashingService.Hash(tempPassword);
        var result = userAccount.AddPassword(PasswordHash.Create(passwordHash), ActorId.Create(_userContext.UserId));

        if (result.IsFailure)
            return Result<ForcePasswordResetResponse>.Failure(result.Error);

        await _userAccountRepository.UpdateAsync(userAccount, cancellationToken);
        await _userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        var email = userAccount.Email.GetValue();
        var notification = NotificationTemplates.PasswordReset(email, email, tempPassword);
        await _notificationService.SendAsync(notification, cancellationToken);

        return Result<ForcePasswordResetResponse>.Success(new ForcePasswordResetResponse(tempPassword));
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%&";
        const string all = upper + lower + digits + special;

        var chars = new char[16];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = special[RandomNumberGenerator.GetInt32(special.Length)];
        for (var i = 4; i < chars.Length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
