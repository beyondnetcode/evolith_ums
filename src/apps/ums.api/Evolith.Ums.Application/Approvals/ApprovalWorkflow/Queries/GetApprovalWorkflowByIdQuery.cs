using Evolith.Ums.Application.Approvals.ApprovalWorkflow.DTOs;

namespace Evolith.Ums.Application.Approvals.ApprovalWorkflow.Queries;

public sealed record GetApprovalWorkflowByIdQuery(Guid ApprovalWorkflowId) : IQuery<ApprovalWorkflowDto>;
