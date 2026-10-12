using Evolith.Ums.Application.Identity.UserManagementDelegation.DTOs;
using Evolith.Ums.Domain.Identity.UserManagementDelegation;

namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Queries;

public sealed class GetDelegationByIdQueryHandler : IQueryHandler<GetDelegationByIdQuery, DelegationDto>
{
    private readonly IUserManagementDelegationRepository _repository;

    public GetDelegationByIdQueryHandler(IUserManagementDelegationRepository repository)
    {
        _repository = repository;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_delegations", ActionCode = "view")]
    public async Task<Result<DelegationDto>> Handle(
        GetDelegationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var delegation = await _repository.GetByIdAsync(request.DelegationId, cancellationToken);

        if (delegation is null)
        {
            return Result<DelegationDto>.Failure("Delegation not found.");
        }

        return Result<DelegationDto>.Success(ToDto(delegation));
    }

    internal static DelegationDto ToDto(Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation d) =>
        new(
            d.GetId().GetValue(),
            d.TenantId.GetValue(),
            d.DelegatingAdminId.GetValue(),
            d.DelegatedAdminId.GetValue(),
            d.ScopeType.Name,
            d.ScopeId,
            d.AllowedActions.Select(a => a.Name).ToList(),
            d.ValidFrom,
            d.ValidUntil,
            d.MaxDurationDays,
            d.RequiresApproval,
            d.ApprovalRequestId,
            d.Status.Name,
            d.RevokedAt,
            d.RevokedBy,
            d.RevocationReason);
}
