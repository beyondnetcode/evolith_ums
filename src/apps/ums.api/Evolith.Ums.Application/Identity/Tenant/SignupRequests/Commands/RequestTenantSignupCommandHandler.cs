using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Application.Identity.Tenant.SignupRequests.DTOs;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity.TenantSignupRequest;
using TenantSignupRequestAggregate = Evolith.Ums.Domain.Identity.TenantSignupRequest.TenantSignupRequest;

namespace Evolith.Ums.Application.Identity.Tenant.SignupRequests.Commands;

public sealed class RequestTenantSignupCommandHandler : ICommandHandler<RequestTenantSignupCommand, RequestTenantSignupResponse>
{
    private readonly ITenantSignupRequestRepository _requestRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly INotificationService _notificationService;

    public RequestTenantSignupCommandHandler(
        ITenantSignupRequestRepository requestRepository,
        ITenantRepository tenantRepository,
        IUserAccountRepository userAccountRepository,
        INotificationService notificationService)
    {
        _requestRepository = requestRepository;
        _tenantRepository = tenantRepository;
        _userAccountRepository = userAccountRepository;
        _notificationService = notificationService;
    }

    [AuthorizationAspect(ResourceCode = "manage_tenants", ActionCode = "manage")]
    public async Task<Result<RequestTenantSignupResponse>> Handle(RequestTenantSignupCommand request, CancellationToken cancellationToken)
    {
        var requestCode = CompanyReference.Create(request.CompanyReference);
        var existingTenant = await _tenantRepository.GetByCodeAsync(requestCode.GetValue(), cancellationToken);
        if (existingTenant is not null)
        {
            return Result<RequestTenantSignupResponse>.Failure("Tenant code already exists.");
        }

        var existingRequest = await _requestRepository.GetAllAsync(cancellationToken);
        if (existingRequest.Any(x => string.Equals(x.CompanyReference.GetValue(), requestCode.GetValue(), StringComparison.OrdinalIgnoreCase)))
        {
            return Result<RequestTenantSignupResponse>.Failure("Tenant signup request already exists.");
        }

        var signupResult = TenantSignupRequestAggregate.Create(
            Name.Create(request.CompanyName),
            requestCode,
            Name.Create(request.ContactName),
            Email.Create(request.ContactEmail),
            ActorId.Create("public-signup"));

        if (signupResult.IsFailure)
        {
            return Result<RequestTenantSignupResponse>.Failure(signupResult.Error);
        }

        var signupRequest = signupResult.Value;
        await _requestRepository.AddAsync(signupRequest, cancellationToken);
        await _requestRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        var superAdminTenant = await _tenantRepository.GetByCodeAsync("INTERNAL_ADMIN", cancellationToken);
        var superAdminUsers = superAdminTenant is null
            ? Array.Empty<Evolith.Ums.Domain.Identity.UserAccount.UserAccount>()
            : await _userAccountRepository.GetByTenantIdAsync(superAdminTenant.Props.Id.GetValue(), cancellationToken);
        var superAdmin = superAdminUsers
            .Where(user => user.Category == UserCategory.Internal && user.Status == UserStatus.Active)
            .OrderBy(user => user.Email.GetValue())
            .FirstOrDefault();

        if (superAdmin is not null)
        {
            await _notificationService.SendAsync(
                NotificationTemplates.TenantSignupRequestReceived(
                    superAdmin.Email.GetValue(),
                    signupRequest.CompanyName.GetValue(),
                    signupRequest.ContactEmail.GetValue()),
                cancellationToken);
        }

        await _notificationService.SendAsync(
            NotificationTemplates.TenantSignupConfirmation(
                signupRequest.ContactEmail.GetValue(),
                signupRequest.CompanyName.GetValue()),
            cancellationToken);

        return Result<RequestTenantSignupResponse>.Success(new RequestTenantSignupResponse(
            signupRequest.GetId().GetValue(),
            "La solicitud de registro fue recibida. El Super Admin la revisará."));
    }
}
