using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Domain.Identity;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed class DenyUserSignupCommandHandler : ICommandHandler<DenyUserSignupCommand>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserContext _userContext;
    private readonly INotificationService _notificationService;

    public DenyUserSignupCommandHandler(
        IUserAccountRepository userAccountRepository,
        ITenantRepository tenantRepository,
        IUserContext userContext,
        INotificationService notificationService)
    {
        _userAccountRepository = userAccountRepository;
        _tenantRepository = tenantRepository;
        _userContext = userContext;
        _notificationService = notificationService;
    }

    [AuthorizationAspect(ResourceCode = "manage_users", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(DenyUserSignupCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required to deny a signup request.");

        var userAccount = await _userAccountRepository.GetByIdAsync(request.UserAccountId, cancellationToken);
        if (userAccount is null)
            return Result.Failure("User account was not found.");

        var result = userAccount.Deny(ActorId.Create(_userContext.UserId), request.Reason);
        if (result.IsFailure)
            return result;

        await _userAccountRepository.UpdateAsync(userAccount, cancellationToken);
        await _userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        var tenant = await _tenantRepository.GetByIdAsync(userAccount.TenantId.GetValue(), cancellationToken);
        if (tenant is not null)
        {
            var recipientName = userAccount.DisplayName?.GetValue() ?? userAccount.Email.GetValue().Split('@')[0];
            await _notificationService.SendAsync(
                NotificationTemplates.UserSignupDenied(
                    userAccount.Email.GetValue(),
                    recipientName,
                    tenant.Name.GetValue(),
                    request.Reason),
                cancellationToken);
        }

        return Result.Success();
    }
}
