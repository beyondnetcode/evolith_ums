using Evolith.Ums.Application.Identity.Tenant.IdentityProvider.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.IdentityProvider.Commands;

public sealed class RegisterIdentityProviderCommandHandler : ICommandHandler<RegisterIdentityProviderCommand, RegisterIdentityProviderResponse>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public RegisterIdentityProviderCommandHandler(
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
    public async Task<Result<RegisterIdentityProviderResponse>> Handle(
        RegisterIdentityProviderCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<RegisterIdentityProviderResponse>.Failure("Authenticated user is required to register an identity provider.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<RegisterIdentityProviderResponse>.Failure("Tenant was not found.");
        }

        var scopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(request.TenantId, cancellationToken);
        if (scopeResult.IsFailure)
        {
            return Result<RegisterIdentityProviderResponse>.Failure(scopeResult.Error);
        }

        var strategy = DomainEnumerationParser.FromName<IdpStrategy>(request.Strategy);
        if (strategy is null)
        {
            return Result<RegisterIdentityProviderResponse>.Failure($"Invalid identity provider strategy: {request.Strategy}");
        }

        var result = tenant.RegisterIdentityProvider(
            Code.Create(request.Code),
            Name.Create(request.Name),
            Description.Create(request.Description),
            strategy,
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure)
        {
            return Result<RegisterIdentityProviderResponse>.Failure(result.Error);
        }

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await _tenantRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        var createdIdp = tenant.IdentityProviders.FirstOrDefault(ip => ip.Code.GetValue() == request.Code);
        return Result<RegisterIdentityProviderResponse>.Success(new RegisterIdentityProviderResponse(
            request.TenantId,
            createdIdp?.GetId().GetValue() ?? Guid.Empty));
    }
}
