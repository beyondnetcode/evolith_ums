using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Identity;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed class SetUserAccountDisplayNameCommandHandler : ICommandHandler<SetUserAccountDisplayNameCommand>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public SetUserAccountDisplayNameCommandHandler(
        IUserAccountRepository userAccountRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy)
    {
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "manage_users", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(SetUserAccountDisplayNameCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to set a display name.");
        }

        var userAccount = await _userAccountRepository.GetByIdAsync(request.UserAccountId, cancellationToken);
        if (userAccount is null)
        {
            return Result.Failure("User account was not found.");
        }

        // El nombre de una persona es un dato personal: se exige el MISMO gate que el resto de la
        // administración de cuentas (Block, ForcePasswordReset, Create). Sin esto, cualquier usuario
        // autenticado podría renombrar cuentas de otro inquilino.
        var scope = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(
            userAccount.TenantId.GetValue(), cancellationToken);
        if (scope.IsFailure)
        {
            return Result.Failure(scope.Error);
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? null
            : Name.Create(request.DisplayName.Trim());

        var result = userAccount.SetDisplayName(displayName, ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _userAccountRepository.UpdateAsync(userAccount, cancellationToken);
        await _userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
