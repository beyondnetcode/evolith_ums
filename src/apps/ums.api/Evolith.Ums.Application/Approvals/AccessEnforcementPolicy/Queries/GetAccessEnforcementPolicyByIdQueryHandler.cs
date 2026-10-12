using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.AccessEnforcementPolicy;

namespace Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Queries;

public sealed class GetAccessEnforcementPolicyByIdQueryHandler : IQueryHandler<GetAccessEnforcementPolicyByIdQuery, AccessEnforcementPolicyDto>
{
    private readonly IAccessEnforcementPolicyRepository _repository;

    public GetAccessEnforcementPolicyByIdQueryHandler(IAccessEnforcementPolicyRepository repository) => _repository = repository;

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_approval_rules", ActionCode = "view")]
    public async Task<Result<AccessEnforcementPolicyDto>> Handle(GetAccessEnforcementPolicyByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.AccessEnforcementPolicyId, cancellationToken);
        if (entity is null) return Result<AccessEnforcementPolicyDto>.Failure("Access enforcement policy not found.");

        return Result<AccessEnforcementPolicyDto>.Success(new AccessEnforcementPolicyDto(
            entity.Props.Id.GetValue(), entity.Props.TenantId.GetValue(), entity.Props.ProfileId?.GetValue(),
            entity.Props.RoleId?.GetValue(), entity.Props.EnforcementAction.ToString(), entity.Props.IsActive,
            entity.Props.GracePeriodDays));
    }
}
