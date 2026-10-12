
namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

public sealed class SubmitDelegationForApprovalCommandHandler : ICommandHandler<SubmitDelegationForApprovalCommand>
{
    private readonly IUserManagementDelegationRepository _repository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public SubmitDelegationForApprovalCommandHandler(
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
    public async Task<Result> Handle(SubmitDelegationForApprovalCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to submit a delegation for approval.");
        }

        var delegation = await _repository.GetByIdAsync(request.DelegationId, cancellationToken);
        if (delegation is null)
        {
            return Result.Failure("Delegation not found.");
        }

        // Autorización + aislamiento por inquilino (G-148/G-149): enviar a aprobación es una
        // operación administrativa sobre la delegación; se exige el mismo gate de management-owner
        // que el resto de comandos de gestión y se veda la operación cruzada entre inquilinos.
        var ownerScope = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(delegation.TenantId.GetValue(), cancellationToken);
        if (ownerScope.IsFailure)
        {
            return ownerScope;
        }

        // Espejo de IGA RolePromotion (ADR-UMS-093): la delegación orquesta su propia máquina de
        // aprobación dentro de su agregado, sin materializar un ApprovalRequest genérico (cuyo
        // contrato Create exige workflow/sistema/rol ajenos a una delegación). El identificador de
        // la solicitud es una correlación generada aquí y congelada en el agregado (INV: inmutable
        // tras Submit). Esto preserva un-agregado-por-transacción (ADR-UMS-086).
        var approvalRequestId = Guid.NewGuid();

        var result = delegation.SubmitForApproval(approvalRequestId, ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(delegation, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
