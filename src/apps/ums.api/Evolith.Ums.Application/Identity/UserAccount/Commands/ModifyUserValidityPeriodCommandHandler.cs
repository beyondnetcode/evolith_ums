using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Application.Common.Interfaces;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed class ModifyUserValidityPeriodCommandHandler : ICommandHandler<ModifyUserValidityPeriodCommand>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantContext _tenantContext;
    private readonly IConfigurationProvider _configProvider;

    public ModifyUserValidityPeriodCommandHandler(
        IUserAccountRepository userAccountRepository,
        IUserContext userContext,
        ITenantContext tenantContext,
        IConfigurationProvider configProvider)
    {
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
        _tenantContext = tenantContext;
        _configProvider = configProvider;
    }

    [AuthorizationAspect(ResourceCode = "manage_users", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ModifyUserValidityPeriodCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var userAccount = await _userAccountRepository.GetByIdAsync(request.UserAccountId, cancellationToken);
        if (userAccount is null)
            return Result.Failure("User account was not found.");

        if (!_tenantContext.IsInternalAdmin &&
            userAccount.TenantId.GetValue() != _tenantContext.OrganizationId)
            return Result.Failure("AUTH_010: Target user is outside the administrator's operational scope.");

        var cfg = _configProvider.ForTenant(userAccount.TenantId.GetValue());
        var maxDays = cfg.MaxValidityPeriodDays;

        var result = userAccount.SetValidityPeriod(request.ExpiresAt, maxDays, ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
            return result;

        await _userAccountRepository.UpdateAsync(userAccount, cancellationToken);
        await _userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
