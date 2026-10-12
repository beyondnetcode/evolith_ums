
namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

public sealed class RejectDelegationCommandHandler : ICommandHandler<RejectDelegationCommand>
{
    private readonly IUserManagementDelegationRepository _repository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public RejectDelegationCommandHandler(
        IUserManagementDelegationRepository repository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy)
    {
        _repository = repository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "manage_delegations", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(RejectDelegationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to reject a delegation.");
        }

        var delegation = await _repository.GetByIdAsync(request.DelegationId, cancellationToken);
        if (delegation is null)
        {
            return Result.Failure("Delegation not found.");
        }

        // Autorización + aislamiento por inquilino (G-148/G-149): mismo gate de management-owner
        // que Approve. Rechazar una solicitud pendiente es una decisión de gobernanza reservada al
        // management-owner/internal-admin; no se admite de forma cruzada entre inquilinos.
        var ownerScope = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(delegation.TenantId.GetValue(), cancellationToken);
        if (ownerScope.IsFailure)
        {
            return ownerScope;
        }

        var result = delegation.Reject(request.Reason, ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(delegation, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
