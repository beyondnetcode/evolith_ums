using Evolith.Ums.Application.Identity.Tenant.DTOs;
using Evolith.Ums.Domain.Kernel;

namespace Evolith.Ums.Application.Identity.Tenant.Commands;

public sealed class SuspendTenantCommandHandler : ICommandHandler<SuspendTenantCommand>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;

    public SuspendTenantCommandHandler(
        ITenantRepository tenantRepository,
        IUserAccountRepository userAccountRepository,
        IUserContext userContext)
    {
        _tenantRepository = tenantRepository;
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_tenants", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(SuspendTenantCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to suspend a tenant.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure("Tenant was not found.");
        }

        // ── Dependency guard: active users ────────────────────────────────────
        var activeUserCount = await _userAccountRepository.CountActiveByTenantAsync(
            request.TenantId, cancellationToken);

        if (activeUserCount > 0)
        {
            var deps = new List<BlockingDependency>
            {
                new("UserAccount", "Active", activeUserCount),
            };
            return Result.Failure(BlockedOperationError.Encode(DomainErrors.Tenant.HasActiveUsers, deps));
        }

        var result = tenant.Suspend(ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await _tenantRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
