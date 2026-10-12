namespace Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;

public sealed record ApprovalRequestDto(
    Guid ApprovalRequestId,
    Guid WorkflowId,
    Guid TargetUserId,
    Guid? TargetProfileId,
    string Status,
    Guid RequestedSystemId,
    Guid? RequestedBranchId,
    Guid RequestedRoleId,
    string? Justification,
    Guid? GrantedRoleId,
    string? DecisionReason,
    string? DecisionBy,
    DateTime? DecisionAt);
