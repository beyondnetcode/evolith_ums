namespace Evolith.Ums.Application.Approvals.ApprovalRequest.Services;

using ApprovalRequestAggregate = Evolith.Ums.Domain.Approvals.ApprovalRequest.ApprovalRequest;
using ApprovalWorkflowAggregate = Evolith.Ums.Domain.Approvals.ApprovalWorkflow.ApprovalWorkflow;

public interface IApprovalRequestCreationPolicyResolver
{
    Result<ApprovalRequestAggregate> Create(
        ApprovalWorkflowAggregate workflow,
        UserId targetUserId,
        ProfileId? targetProfileId,
        SystemSuiteId requestedSystemId,
        BranchId? requestedBranchId,
        RoleId requestedRoleId,
        string? justification,
        ActorId actorId);
}
