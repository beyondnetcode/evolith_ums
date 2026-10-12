using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.Branch.Commands;

public sealed class DeactivateBranchCommandHandler : ICommandHandler<DeactivateBranchCommand, DeactivateBranchResponse>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public DeactivateBranchCommandHandler(
        ITenantRepository tenantRepository,
        IUserAccountRepository userAccountRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy)
    {
        _tenantRepository = tenantRepository;
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "manage_tenants", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<DeactivateBranchResponse>> Handle(
        DeactivateBranchCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<DeactivateBranchResponse>.Failure("Authenticated user is required to deactivate a branch.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<DeactivateBranchResponse>.Failure("Tenant was not found.");
        }

        var scopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(request.TenantId, cancellationToken);
        if (scopeResult.IsFailure)
        {
            return Result<DeactivateBranchResponse>.Failure(scopeResult.Error);
        }

        // ── Dependency guard: active users bound to the branch (G-046) ────────
        var activeUserCount = await _userAccountRepository.CountActiveByBranchAsync(
            request.BranchId, cancellationToken);

        if (activeUserCount > 0)
        {
            var deps = new List<BlockingDependency>
            {
                new("UserAccount", "Active", activeUserCount),
            };
            return Result<DeactivateBranchResponse>.Failure(
                BlockedOperationError.Encode(DomainErrors.Tenant.BranchHasActiveUsers, deps));
        }

        var result = tenant.DeactivateBranch(
            IdValueObject.Load(request.BranchId),
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure)
        {
            return Result<DeactivateBranchResponse>.Failure(result.Error);
        }

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await _tenantRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<DeactivateBranchResponse>.Success(new DeactivateBranchResponse(request.TenantId));
    }
}
