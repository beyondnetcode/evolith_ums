using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;

namespace Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Commands;

using Evolith.Ums.Domain;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.AccessEnforcementPolicy;
using Evolith.Ums.Domain.Enums;

public sealed class CreateAccessEnforcementPolicyCommandHandler : ICommandHandler<CreateAccessEnforcementPolicyCommand, CreateAccessEnforcementPolicyResponse>
{
    private readonly IAccessEnforcementPolicyRepository _repository;
    private readonly IUserContext _userContext;

    public CreateAccessEnforcementPolicyCommandHandler(IAccessEnforcementPolicyRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_approval_rules", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateAccessEnforcementPolicyResponse>> Handle(CreateAccessEnforcementPolicyCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result<CreateAccessEnforcementPolicyResponse>.Failure("Authenticated user is required.");

        var action = DomainEnumerationParser.FromName<AccessEnforcementAction>(request.EnforcementAction)!;

        var result = AccessEnforcementPolicy.Create(
            TenantId.Load(request.TenantId),
            request.ProfileId.HasValue ? ProfileId.Load(request.ProfileId.Value) : null,
            request.RoleId.HasValue ? RoleId.Load(request.RoleId.Value) : null,
            action,
            ActorId.Create(_userContext.UserId),
            request.GracePeriodDays);

        if (result.IsFailure) return Result<CreateAccessEnforcementPolicyResponse>.Failure(result.Error);

        await _repository.AddAsync(result.Value, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateAccessEnforcementPolicyResponse>.Success(new CreateAccessEnforcementPolicyResponse(result.Value.Props.Id.GetValue()));
    }
}
