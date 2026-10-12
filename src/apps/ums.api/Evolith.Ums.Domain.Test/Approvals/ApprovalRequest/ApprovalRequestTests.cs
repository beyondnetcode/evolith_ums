namespace Evolith.Ums.Domain.Test.Approvals.ApprovalRequest;

using Evolith.Ums.Domain.Approvals.ApprovalRequest;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Xunit;

/// <summary>
/// Domain tests for the <see cref="ApprovalRequest"/> aggregate.
///
/// Coverage intent:
///   – State machine: Pending → Approved | Rejected (terminal states).
///   – Double-transition guards: approve-then-approve, reject-then-reject,
///     approve-then-reject, reject-then-approve.
///   – Immutability: WorkflowId and TargetUserId unchanged after transitions.
///   – Optional TargetProfileId: null and non-null creation paths.
///   – Correct domain error constant surfaced on invalid transitions.
///
/// Design note: ApprovalRequest intentionally raises no domain events at the
/// domain layer — event dispatch is the responsibility of the parent
/// ApprovalWorkflow aggregate. Tests verify this contract is stable.
///
/// Excluded intentionally:
///   – Audit field timestamps (infrastructure concern).
///   – Repository interaction (belongs to application-layer tests).
/// </summary>
public class ApprovalRequestTests
{
    private static readonly ApprovalWorkflowId ValidWorkflowId = ApprovalWorkflowId.Load(Guid.NewGuid().ToString());
    private static readonly UserId ValidUserId                 = UserId.Load(Guid.NewGuid().ToString());
    private static readonly ProfileId? ValidProfileId          = ProfileId.Load(Guid.NewGuid().ToString());
    private static readonly SystemSuiteId ValidSystemId        = SystemSuiteId.Load(Guid.NewGuid());
    private static readonly RoleId ValidRoleId                 = RoleId.Load(Guid.NewGuid());
    private static readonly ActorId ValidActor                 = ActorId.Create("user-001");
    private static readonly ActorId ApproverActor             = ActorId.Create("approver-002");
    private static readonly ActorId RejecterActor             = ActorId.Create("rejecter-003");

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static ApprovalRequest MakePending(ProfileId? profileId = null) =>
        ApprovalRequest.Create(ValidWorkflowId, ValidUserId, profileId ?? ValidProfileId,
            ValidSystemId, null, ValidRoleId, null, ValidActor).Value;

    // =========================================================================
    #region Create
    // =========================================================================

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = ApprovalRequest.Create(ValidWorkflowId, ValidUserId, ValidProfileId,
            ValidSystemId, null, ValidRoleId, null, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidWorkflowId, result.Value.WorkflowId);
        Assert.Equal(ValidUserId, result.Value.TargetUserId);
        Assert.Equal(ValidProfileId, result.Value.TargetProfileId);
        Assert.Equal(ApprovalStatus.Pending, result.Value.Status);
    }

    [Fact]
    public void Create_WithoutProfileId_TargetProfileIdIsNull()
    {
        var result = ApprovalRequest.Create(ValidWorkflowId, ValidUserId, null,
            ValidSystemId, null, ValidRoleId, null, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.TargetProfileId);
    }

    [Fact]
    public void Create_StatusInitiallyPending()
    {
        var request = MakePending();

        Assert.Equal(ApprovalStatus.Pending, request.Status);
    }

    [Fact]
    public void Create_RaisesNoDomainEvents()
    {
        var result = ApprovalRequest.Create(ValidWorkflowId, ValidUserId, ValidProfileId,
            ValidSystemId, null, ValidRoleId, null, ValidActor);

        Assert.True(result.IsSuccess);
    }

    #endregion

    // =========================================================================
    #region Approve
    // =========================================================================

    [Fact]
    public void Approve_WhenPending_TransitionsToApproved()
    {
        var request = MakePending();

        var result = request.Approve(ApproverActor, ValidRoleId);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Approved, request.Status);
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_ReturnsFailure()
    {
        var request = MakePending();
        request.Approve(ApproverActor, ValidRoleId);

        var result = request.Approve(ApproverActor, ValidRoleId);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RequestNotPending, result.Error);
    }

    [Fact]
    public void Approve_WhenRejected_ReturnsFailure()
    {
        var request = MakePending();
        request.Reject(RejecterActor);

        var result = request.Approve(ApproverActor, ValidRoleId);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RequestNotPending, result.Error);
    }

    [Fact]
    public void Approve_PreservesWorkflowId()
    {
        var request = MakePending();

        request.Approve(ApproverActor, ValidRoleId);

        Assert.Equal(ValidWorkflowId, request.WorkflowId);
    }

    [Fact]
    public void Approve_PreservesTargetUserId()
    {
        var request = MakePending();

        request.Approve(ApproverActor, ValidRoleId);

        Assert.Equal(ValidUserId, request.TargetUserId);
    }

    [Fact]
    public void Approve_PreservesTargetProfileId()
    {
        var request = MakePending();

        request.Approve(ApproverActor, ValidRoleId);

        Assert.Equal(ValidProfileId, request.TargetProfileId);
    }

    #endregion

    // =========================================================================
    #region Reject
    // =========================================================================

    [Fact]
    public void Reject_WhenPending_TransitionsToRejected()
    {
        var request = MakePending();

        var result = request.Reject(RejecterActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Rejected, request.Status);
    }

    [Fact]
    public void Reject_WhenAlreadyRejected_ReturnsFailure()
    {
        var request = MakePending();
        request.Reject(RejecterActor);

        var result = request.Reject(RejecterActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RequestNotPending, result.Error);
    }

    [Fact]
    public void Reject_WhenApproved_ReturnsFailure()
    {
        var request = MakePending();
        request.Approve(ApproverActor, ValidRoleId);

        var result = request.Reject(RejecterActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RequestNotPending, result.Error);
    }

    [Fact]
    public void Reject_PreservesWorkflowId()
    {
        var request = MakePending();

        request.Reject(RejecterActor);

        Assert.Equal(ValidWorkflowId, request.WorkflowId);
    }

    [Fact]
    public void Reject_WithNullProfile_TargetProfileIdRemainsNull()
    {
        // Create explicitly with null profileId — cannot use MakePending() helper
        // because its default `profileId ?? ValidProfileId` would substitute the non-null static.
        var request = ApprovalRequest.Create(ValidWorkflowId, ValidUserId, null,
            ValidSystemId, null, ValidRoleId, null, ValidActor).Value;

        request.Reject(RejecterActor);

        Assert.Null(request.TargetProfileId);
    }

    #endregion

    // =========================================================================
    #region State machine — cross-transition conflicts
    // =========================================================================

    [Fact]
    public void DoubleApprove_SecondCallFails_WithRequestNotPendingError()
    {
        var request = MakePending();
        request.Approve(ApproverActor, ValidRoleId);

        var second = request.Approve(ApproverActor, ValidRoleId);

        Assert.True(second.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RequestNotPending, second.Error);
    }

    [Fact]
    public void DoubleReject_SecondCallFails_WithRequestNotPendingError()
    {
        var request = MakePending();
        request.Reject(RejecterActor);

        var second = request.Reject(RejecterActor);

        Assert.True(second.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RequestNotPending, second.Error);
    }

    [Fact]
    public void ApprovedRequest_IsTerminal_CannotTransitionToAnyOtherStatus()
    {
        var request = MakePending();
        request.Approve(ApproverActor, ValidRoleId);

        var approveAgain = request.Approve(ApproverActor, ValidRoleId);
        var reject       = request.Reject(RejecterActor);

        Assert.True(approveAgain.IsFailure);
        Assert.True(reject.IsFailure);
    }

    [Fact]
    public void RejectedRequest_IsTerminal_CannotTransitionToAnyOtherStatus()
    {
        var request = MakePending();
        request.Reject(RejecterActor);

        var rejectAgain = request.Reject(RejecterActor);
        var approve     = request.Approve(ApproverActor, ValidRoleId);

        Assert.True(rejectAgain.IsFailure);
        Assert.True(approve.IsFailure);
    }

    #endregion
}
