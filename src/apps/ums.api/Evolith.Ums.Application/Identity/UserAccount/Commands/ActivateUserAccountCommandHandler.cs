using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Application.Identity.UserAccount.DTOs;
using Evolith.Ums.Domain.Identity;
using NotificationTemplates = Evolith.Ums.Application.Common.Notifications.NotificationTemplates;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed class ActivateUserAccountCommandHandler : ICommandHandler<ActivateUserAccountCommand>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantRepository _tenantRepository;
    private readonly INotificationService _notificationService;

    public ActivateUserAccountCommandHandler(
        IUserAccountRepository userAccountRepository,
        IUserContext userContext,
        ITenantRepository tenantRepository,
        INotificationService notificationService)
    {
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
        _tenantRepository = tenantRepository;
        _notificationService = notificationService;
    }

    [AuthorizationAspect(ResourceCode = "manage_users", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ActivateUserAccountCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to activate a user account.");
        }

        var userAccount = await _userAccountRepository.GetByIdAsync(request.UserAccountId, cancellationToken);
        if (userAccount is null)
        {
            return Result.Failure("User account was not found.");
        }

        var result = userAccount.Activate(ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _userAccountRepository.UpdateAsync(userAccount, cancellationToken);
        await _userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        var tenant = await _tenantRepository.GetByIdAsync(userAccount.TenantId.GetValue(), cancellationToken);
        if (tenant is not null)
        {
            var recipientName = userAccount.DisplayName?.GetValue() ?? userAccount.Email.GetValue().Split('@')[0];
            await _notificationService.SendAsync(
                NotificationTemplates.UserSignupApproved(
                    userAccount.Email.GetValue(),
                    recipientName,
                    tenant.Name.GetValue()),
                cancellationToken);
        }

        return Result.Success();
    }
}
