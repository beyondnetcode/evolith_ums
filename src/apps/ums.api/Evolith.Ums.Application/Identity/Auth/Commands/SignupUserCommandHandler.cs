using System.Security.Cryptography;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity.UserAccount;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

namespace Evolith.Ums.Application.Identity.Auth.Commands;

public sealed class SignupUserCommandHandler : ICommandHandler<SignupUserCommand, UserSignupResponse>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly INotificationService _notificationService;

    public SignupUserCommandHandler(
        ITenantRepository tenantRepository,
        IUserAccountRepository userAccountRepository,
        IPasswordHashingService passwordHashingService,
        INotificationService notificationService)
    {
        _tenantRepository = tenantRepository;
        _userAccountRepository = userAccountRepository;
        _passwordHashingService = passwordHashingService;
        _notificationService = notificationService;
    }

    public async Task<Result<UserSignupResponse>> Handle(SignupUserCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByCodeAsync(request.TenantCode, cancellationToken);
        if (tenant is null)
        {
            return Result<UserSignupResponse>.Failure("Tenant was not found.");
        }

        var email = Email.Create(request.Email);
        var existingUser = await _userAccountRepository.GetByEmailAsync(email, cancellationToken);
        if (existingUser is not null)
        {
            return Result<UserSignupResponse>.Failure("User email already exists.");
        }

        var tenantUsers = await _userAccountRepository.GetByTenantIdAsync(tenant.Props.Id.GetValue(), cancellationToken);
        var tenantAdmin = tenantUsers
            .Where(user => user.Category == UserCategory.Internal && user.Status == UserStatus.Active)
            .OrderBy(user => user.Email.GetValue())
            .FirstOrDefault();

        if (tenantAdmin is null)
        {
            return Result<UserSignupResponse>.Failure("No active tenant admin found to receive the request.");
        }

        var passwordHash = PasswordHash.Create(_passwordHashingService.Hash(request.Password));
        var userAccountResult = UserAccountAggregate.Create(
            TenantId.Load(tenant.Props.Id.GetValue()),
            email,
            UserCategory.External,
            null,
            null,
            ActorId.Create("public-signup"),
            displayName: Name.Create(request.DisplayName));

        if (userAccountResult.IsFailure)
        {
            return Result<UserSignupResponse>.Failure(userAccountResult.Error);
        }

        var userAccount = userAccountResult.Value;
        var passwordResult = userAccount.AddPassword(passwordHash, ActorId.Create("public-signup"));
        if (passwordResult.IsFailure)
        {
            return Result<UserSignupResponse>.Failure(passwordResult.Error);
        }

        await _userAccountRepository.AddAsync(userAccount, cancellationToken);
        await _userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        var notifications = new[]
        {
            NotificationTemplates.UserSignupRequestReceived(
                tenantAdmin.Email.GetValue(),
                userAccount.Email.GetValue(),
                tenant.Name.GetValue()),
            NotificationTemplates.UserSignupConfirmation(
                userAccount.Email.GetValue(),
                tenant.Name.GetValue())
        };

        await _notificationService.SendBulkAsync(notifications, cancellationToken);

        return Result<UserSignupResponse>.Success(new UserSignupResponse(
            userAccount.Props.Id.GetValue(),
            "Su solicitud de acceso fue recibida. El administrador del tenant la revisará."));
    }
}
