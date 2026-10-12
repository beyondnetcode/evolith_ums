using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;

namespace Evolith.Ums.Application.Approvals.ApprovalRequest.Queries;

public sealed record GetApprovalRequestByIdQuery(Guid ApprovalRequestId) : IQuery<ApprovalRequestDto>;
