using System.Security.Cryptography;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Application.Identity.Tenant.SignupRequests.DTOs;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity.TenantSignupRequest;
using Evolith.Ums.Domain.Identity.UserAccount;
using NotificationTemplates = Evolith.Ums.Application.Common.Notifications.NotificationTemplates;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

namespace Evolith.Ums.Application.Identity.Tenant.SignupRequests.Commands;

public sealed class ApproveTenantSignupCommandHandler : ICommandHandler<ApproveTenantSignupCommand, ApproveTenantSignupResponse>
{
    private readonly ITenantSignupRequestRepository _requestRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantContext _tenantContext;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly INotificationService _notificationService;

    public ApproveTenantSignupCommandHandler(
        ITenantSignupRequestRepository requestRepository,
        ITenantRepository tenantRepository,
        IUserAccountRepository userAccountRepository,
        IUserContext userContext,
        ITenantContext tenantContext,
        IPasswordHashingService passwordHashingService,
        INotificationService notificationService)
    {
        _requestRepository = requestRepository;
        _tenantRepository = tenantRepository;
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
        _tenantContext = tenantContext;
        _passwordHashingService = passwordHashingService;
        _notificationService = notificationService;
    }

    [AuthorizationAspect(ResourceCode = "manage_tenants", ActionCode = "manage")]
    public async Task<Result<ApproveTenantSignupResponse>> Handle(ApproveTenantSignupCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsInternalAdmin)
        {
            return Result<ApproveTenantSignupResponse>.Failure("Internal admin access is required.");
        }

        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<ApproveTenantSignupResponse>.Failure("Authenticated user is required.");
        }

        var signupRequest = await _requestRepository.GetByIdAsync(request.TenantSignupRequestId, cancellationToken);
        if (signupRequest is null)
        {
            return Result<ApproveTenantSignupResponse>.Failure("Tenant signup request was not found.");
        }

        var tenantCode = Code.Create(signupRequest.CompanyReference.GetValue());
        var existingTenant = await _tenantRepository.GetByCodeAsync(tenantCode.GetValue(), cancellationToken);
        if (existingTenant is not null)
        {
            return Result<ApproveTenantSignupResponse>.Failure("Tenant code already exists.");
        }

        var password = GenerateTemporaryPassword();
        var tenantResult = Evolith.Ums.Domain.Identity.Tenant.Tenant.Create(
            tenantCode,
            Name.Create(signupRequest.CompanyName.GetValue()),
            OrganizationType.CLIENT,
            ActorId.Create(_userContext.UserId),
            IdpStrategy.InternalBcrypt,
            CompanyReference.Create(signupRequest.CompanyReference.GetValue()));

        if (tenantResult.IsFailure)
        {
            return Result<ApproveTenantSignupResponse>.Failure(tenantResult.Error);
        }

        var tenant = tenantResult.Value;
        var userResult = UserAccountAggregate.Create(
            TenantId.Load(tenant.Props.Id.GetValue()),
            Email.Create(signupRequest.ContactEmail.GetValue()),
            UserCategory.Internal,
            null,
            null,
            ActorId.Create(_userContext.UserId),
            displayName: Name.Create(signupRequest.ContactName.GetValue()));

        if (userResult.IsFailure)
        {
            return Result<ApproveTenantSignupResponse>.Failure(userResult.Error);
        }

        var adminUser = userResult.Value;
        var passwordHash = PasswordHash.Create(_passwordHashingService.Hash(password));
        var passwordResult = adminUser.AddPassword(passwordHash, ActorId.Create(_userContext.UserId));
        if (passwordResult.IsFailure)
        {
            return Result<ApproveTenantSignupResponse>.Failure(passwordResult.Error);
        }

        var activateResult = adminUser.Activate(ActorId.Create(_userContext.UserId));
        if (activateResult.IsFailure)
        {
            return Result<ApproveTenantSignupResponse>.Failure(activateResult.Error);
        }

        var approveResult = signupRequest.Approve(TenantId.Load(tenant.Props.Id.GetValue()), ActorId.Create(_userContext.UserId));
        if (approveResult.IsFailure)
        {
            return Result<ApproveTenantSignupResponse>.Failure(approveResult.Error);
        }

        // TODO(D-016): excepción JUSTIFICADA a "un agregado por transacción" (ADR-0098 D2):
        // crea Tenant + UserAccount + actualiza SignupRequest en la misma tx. El arranque de un
        // inquilino con su primer admin es indivisible (crearlo a medias deja un inquilino inusable),
        // por lo que se permite consistencia inmediata bajo ADR-0098 D5. Ver DECISIONS.md D-016 (E1) y GAPS.md G-066.
        // Patrón e interpretación: KB-TXN-001 (Base de Conocimiento de Arquitectura, evolith-core).
        // Al progresar a microservicios, separar vía saga de aprovisionamiento.
        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await _userAccountRepository.AddAsync(adminUser, cancellationToken);
        await _requestRepository.UpdateAsync(signupRequest, cancellationToken);
        await _requestRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        await _notificationService.SendAsync(
            NotificationTemplates.TenantSignupApproved(
                signupRequest.ContactEmail.GetValue(),
                signupRequest.CompanyName.GetValue(),
                adminUser.Email.GetValue(),
                password),
            cancellationToken);

        return Result<ApproveTenantSignupResponse>.Success(new ApproveTenantSignupResponse(
            tenant.Props.Id.GetValue(),
            adminUser.Props.Id.GetValue(),
            password,
            "El tenant fue aprobado y se generaron las credenciales de administración."));
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
