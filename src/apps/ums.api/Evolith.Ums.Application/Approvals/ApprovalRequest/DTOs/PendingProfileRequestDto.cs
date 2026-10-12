namespace Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;

public sealed record PendingProfileRequestDto(
    Guid ApprovalRequestId,
    Guid TargetUserId,
    Guid RequestedSystemId,
    Guid? RequestedBranchId,
    Guid RequestedRoleId,
    string? Justification,
    DateTime RequestedAt);
