using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;

namespace Evolith.Ums.Application.Approvals.ApprovalRequest.Commands;

public sealed record CreateApprovalRequestCommand(
    Guid WorkflowId,
    Guid TargetUserId,
    Guid? TargetProfileId,
    Guid RequestedSystemId,
    Guid? RequestedBranchId,
    Guid RequestedRoleId,
    string? Justification) : ICommand<CreateApprovalRequestResponse>;
