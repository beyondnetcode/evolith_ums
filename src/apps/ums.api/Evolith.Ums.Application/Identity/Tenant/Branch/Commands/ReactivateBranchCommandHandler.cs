using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.Branch.Commands;

public sealed class ReactivateBranchCommandHandler : ICommandHandler<ReactivateBranchCommand, ReactivateBranchResponse>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public ReactivateBranchCommandHandler(
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
    public async Task<Result<ReactivateBranchResponse>> Handle(
        ReactivateBranchCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<ReactivateBranchResponse>.Failure("Authenticated user is required to reactivate a branch.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<ReactivateBranchResponse>.Failure("Tenant was not found.");
        }

        var scopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(request.TenantId, cancellationToken);
        if (scopeResult.IsFailure)
        {
            return Result<ReactivateBranchResponse>.Failure(scopeResult.Error);
        }

        var result = tenant.ReactivateBranch(
            IdValueObject.Load(request.BranchId),
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure)
        {
            return Result<ReactivateBranchResponse>.Failure(result.Error);
        }

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await _tenantRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<ReactivateBranchResponse>.Success(new ReactivateBranchResponse(request.TenantId));
    }
}
