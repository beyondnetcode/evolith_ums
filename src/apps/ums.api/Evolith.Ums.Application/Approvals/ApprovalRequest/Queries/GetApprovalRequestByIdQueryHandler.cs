using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.ApprovalRequest;
using Evolith.Ums.Domain.Enums;

namespace Evolith.Ums.Application.Approvals.ApprovalRequest.Queries;

public sealed class GetApprovalRequestByIdQueryHandler : IQueryHandler<GetApprovalRequestByIdQuery, ApprovalRequestDto>
{
    private readonly IApprovalRequestRepository _repository;

    public GetApprovalRequestByIdQueryHandler(IApprovalRequestRepository repository) => _repository = repository;

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_approvals", ActionCode = "view")]
    public async Task<Result<ApprovalRequestDto>> Handle(GetApprovalRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.ApprovalRequestId, cancellationToken);
        if (entity is null) return Result<ApprovalRequestDto>.Failure("Approval request not found.");

        var audit = entity.Props.Audit.GetValue();
        return Result<ApprovalRequestDto>.Success(new ApprovalRequestDto(
            entity.Props.Id.GetValue(), entity.Props.WorkflowId.GetValue(), entity.Props.TargetUserId.GetValue(),
            entity.Props.TargetProfileId?.GetValue(), entity.Props.Status == ApprovalStatus.Rejected ? "Denied" : entity.Props.Status.ToString(),
            entity.RequestedSystemId.GetValue(), entity.RequestedBranchId?.GetValue(), entity.RequestedRoleId.GetValue(),
            entity.Justification, entity.GrantedRoleId?.GetValue(), entity.DecisionReason,
            audit.UpdatedBy, audit.UpdatedAt));
    }
}
