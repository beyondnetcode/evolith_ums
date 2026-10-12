
namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

public sealed class ExpireDelegationCommandHandler : ICommandHandler<ExpireDelegationCommand>
{
    private readonly IUserManagementDelegationRepository _repository;
    private readonly IUserContext _userContext;

    public ExpireDelegationCommandHandler(
        IUserManagementDelegationRepository repository,
        IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_delegations", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ExpireDelegationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to expire a delegation.");
        }

        var delegation = await _repository.GetByIdAsync(request.DelegationId, cancellationToken);
        if (delegation is null)
        {
            return Result.Failure("Delegation not found.");
        }

        var result = delegation.Expire(ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(delegation, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
