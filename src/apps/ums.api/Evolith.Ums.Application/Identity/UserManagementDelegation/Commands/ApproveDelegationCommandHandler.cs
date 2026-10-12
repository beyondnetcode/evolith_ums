
namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

public sealed class ApproveDelegationCommandHandler : ICommandHandler<ApproveDelegationCommand>
{
    private readonly IUserManagementDelegationRepository _repository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public ApproveDelegationCommandHandler(
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
    public async Task<Result> Handle(ApproveDelegationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to approve a delegation.");
        }

        var delegation = await _repository.GetByIdAsync(request.DelegationId, cancellationToken);
        if (delegation is null)
        {
            return Result.Failure("Delegation not found.");
        }

        // Autorización + aislamiento por inquilino (G-148/G-149): aprobar una delegación es una
        // operación administrativa (concede autoridad sobre cuentas del inquilino), así que se
        // exige el mismo gate de management-owner que Block/Create/ForcePasswordReset de UserAccount
        // (ADR-UMS-086 endurecido). Cierra: solo management-owner/internal-admin aprueba, y nunca
        // de forma cruzada entre inquilinos. Sin fallback de acceso delegado a propósito: un
        // administrador delegado no puede aprobar delegaciones (evita escalada).
        var ownerScope = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(delegation.TenantId.GetValue(), cancellationToken);
        if (ownerScope.IsFailure)
        {
            return ownerScope;
        }

        var result = delegation.Approve(ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(delegation, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
