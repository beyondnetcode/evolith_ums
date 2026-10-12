namespace Evolith.Ums.Domain.Approvals.ApprovalRequest;

public class ApprovalRequestProps : IProps
{
    public IdValueObject Id { get; set; }
    public ApprovalWorkflowId WorkflowId { get; set; }
    public UserId TargetUserId { get; set; }
    public ProfileId? TargetProfileId { get; set; }
    public ApprovalStatus Status { get; set; }

    // What the user declared when submitting the profile request
    public SystemSuiteId RequestedSystemId { get; set; }
    public BranchId? RequestedBranchId { get; set; }
    public RoleId RequestedRoleId { get; set; }
    public string? Justification { get; set; }

    // What the approver decided
    public RoleId? GrantedRoleId { get; set; }
    public string? DecisionReason { get; set; }

    public AuditValueObject Audit { get; private set; }

    public ApprovalRequestProps(
        IdValueObject id,
        ApprovalWorkflowId workflowId,
        UserId targetUserId,
        ProfileId? targetProfileId,
        ApprovalStatus status,
        SystemSuiteId requestedSystemId,
        BranchId? requestedBranchId,
        RoleId requestedRoleId,
        string? justification,
        ActorId createdBy)
    {
        Id = id;
        WorkflowId = workflowId;
        TargetUserId = targetUserId;
        TargetProfileId = targetProfileId;
        Status = status;
        RequestedSystemId = requestedSystemId;
        RequestedBranchId = requestedBranchId;
        RequestedRoleId = requestedRoleId;
        Justification = justification;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public object Clone() => MemberwiseClone();
}
