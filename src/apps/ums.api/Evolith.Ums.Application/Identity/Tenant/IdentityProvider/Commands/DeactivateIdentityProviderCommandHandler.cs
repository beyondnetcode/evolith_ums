using Evolith.Ums.Application.Identity.Tenant.IdentityProvider.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.IdentityProvider.Commands;

public sealed class DeactivateIdentityProviderCommandHandler : ICommandHandler<DeactivateIdentityProviderCommand, DeactivateIdentityProviderResponse>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public DeactivateIdentityProviderCommandHandler(
        ITenantRepository tenantRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy)
    {
        _tenantRepository = tenantRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "manage_tenants", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<DeactivateIdentityProviderResponse>> Handle(
        DeactivateIdentityProviderCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<DeactivateIdentityProviderResponse>.Failure("Authenticated user is required to deactivate an identity provider.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<DeactivateIdentityProviderResponse>.Failure("Tenant was not found.");
        }

        var scopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(request.TenantId, cancellationToken);
        if (scopeResult.IsFailure)
        {
            return Result<DeactivateIdentityProviderResponse>.Failure(scopeResult.Error);
        }

        var result = tenant.DeactivateIdentityProvider(
            IdValueObject.Load(request.IdentityProviderId),
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure)
        {
            return Result<DeactivateIdentityProviderResponse>.Failure(result.Error);
        }

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await _tenantRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<DeactivateIdentityProviderResponse>.Success(new DeactivateIdentityProviderResponse(request.TenantId));
    }
}
