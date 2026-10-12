using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.ApprovalRequest;

namespace Evolith.Ums.Application.Approvals.ApprovalRequest.Queries;

public sealed class GetPendingProfileRequestsQueryHandler
    : IQueryHandler<GetPendingProfileRequestsQuery, IReadOnlyList<PendingProfileRequestDto>>
{
    private readonly IApprovalRequestRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetPendingProfileRequestsQueryHandler(
        IApprovalRequestRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    [AuthorizationAspect(ResourceCode = "view_approvals", ActionCode = "view")]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<IReadOnlyList<PendingProfileRequestDto>>> Handle(
        GetPendingProfileRequestsQuery request, CancellationToken cancellationToken)
    {
        if (_tenantContext.OrganizationId is null)
            return Result<IReadOnlyList<PendingProfileRequestDto>>.Failure("Tenant context is required.");

        var requests = await _repository.GetByTenantIdAsync(_tenantContext.OrganizationId.Value, cancellationToken);

        var pending = requests
            .Where(r => r.Status == ApprovalStatus.Pending)
            .Select(r => new PendingProfileRequestDto(
                r.Props.Id.GetValue(),
                r.TargetUserId.GetValue(),
                r.RequestedSystemId.GetValue(),
                r.RequestedBranchId?.GetValue(),
                r.RequestedRoleId.GetValue(),
                r.Justification,
                r.Props.Audit.GetValue().CreatedAt))
            .OrderBy(r => r.RequestedAt)
            .ToList();

        return Result<IReadOnlyList<PendingProfileRequestDto>>.Success(pending);
    }
}
