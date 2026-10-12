namespace Evolith.Ums.Domain.Approvals.ApprovalRequest;

using Evolith.Ums.Domain.Approvals.ApprovalRequest.Events;

public sealed class ApprovalRequest : AggregateRoot<ApprovalRequest, ApprovalRequestProps>
{
    public new ApprovalRequestDomainEventsManager DomainEvents { get; }

    private ApprovalRequest(ApprovalRequestProps props) : base(props)
    {
        DomainEvents = new ApprovalRequestDomainEventsManager(this);
    }

    public ApprovalWorkflowId WorkflowId => Props.WorkflowId;
    public UserId TargetUserId => Props.TargetUserId;
    public ProfileId? TargetProfileId => Props.TargetProfileId;
    public ApprovalStatus Status => Props.Status;
    public SystemSuiteId RequestedSystemId => Props.RequestedSystemId;
    public BranchId? RequestedBranchId => Props.RequestedBranchId;
    public RoleId RequestedRoleId => Props.RequestedRoleId;
    public string? Justification => Props.Justification;
    public RoleId? GrantedRoleId => Props.GrantedRoleId;
    public string? DecisionReason => Props.DecisionReason;

    public ApprovalRequestId GetId() => ApprovalRequestId.Load(Props.Id.GetValue());

    public static Result<ApprovalRequest> Create(
        ApprovalWorkflowId workflowId,
        UserId targetUserId,
        ProfileId? targetProfileId,
        SystemSuiteId requestedSystemId,
        BranchId? requestedBranchId,
        RoleId requestedRoleId,
        string? justification,
        ActorId createdBy)
    {
        var props = new ApprovalRequestProps(
            IdValueObject.Create(),
            workflowId,
            targetUserId,
            targetProfileId,
            ApprovalStatus.Pending,
            requestedSystemId,
            requestedBranchId,
            requestedRoleId,
            justification,
            createdBy);

        var request = new ApprovalRequest(props);

        if (!request.IsValid())
            return Result<ApprovalRequest>.Failure(request.BrokenRules.GetBrokenRulesAsString());

        return Result<ApprovalRequest>.Success(request);
    }

    public Result Approve(ActorId approvedBy, RoleId grantedRoleId, string? decisionReason = null)
    {
        if (Status != ApprovalStatus.Pending)
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Approvals.RequestNotPending));

        if (!IsValid())
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());

        Props.Status = ApprovalStatus.Approved;
        Props.GrantedRoleId = grantedRoleId;
        Props.DecisionReason = decisionReason;
        // El agregado no emite eventos de dominio en la transicion: el despacho es
        // responsabilidad del handler/flujo de aprobacion. Emitirlos aqui provocaba
        // la publicacion previa al guardado y dejaba la solicitud en Pending (G-051).
        TrackingState.MarkAsDirty();
        Props.Audit.Update(approvedBy.GetValue());
        return Result.Success();
    }

    public Result Reject(ActorId rejectedBy, string? decisionReason = null)
    {
        if (Status != ApprovalStatus.Pending)
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Approvals.RequestNotPending));

        if (!IsValid())
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());

        Props.Status = ApprovalStatus.Rejected;
        Props.DecisionReason = decisionReason;
        // Simetrico con Approve: la transicion no emite eventos de dominio; el
        // despacho corresponde al handler/flujo de aprobacion (G-051).
        TrackingState.MarkAsDirty();
        Props.Audit.Update(rejectedBy.GetValue());
        return Result.Success();
    }
}
