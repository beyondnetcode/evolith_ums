namespace Evolith.Ums.Infrastructure.Approvals.ApprovalRequest;

using ApprovalWorkflowAggregate = Evolith.Ums.Domain.Approvals.ApprovalWorkflow.ApprovalWorkflow;

internal sealed record ApprovalRequestCreationContext(
    ApprovalWorkflowAggregate Workflow,
    UserId TargetUserId,
    ProfileId? TargetProfileId,
    SystemSuiteId RequestedSystemId,
    BranchId? RequestedBranchId,
    RoleId RequestedRoleId,
    string? Justification,
    ActorId ActorId);
