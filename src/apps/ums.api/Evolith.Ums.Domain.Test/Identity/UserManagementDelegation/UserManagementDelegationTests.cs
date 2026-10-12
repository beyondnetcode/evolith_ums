namespace Evolith.Ums.Domain.Test.Identity.UserManagementDelegation;

using Evolith.Ums.Domain.Identity.UserManagementDelegation;
using Xunit;

public class UserManagementDelegationEntityTests
{
    private static readonly TenantId ValidTenantId = TenantId.Create();
    private static readonly UserAccountId ValidDelegatingAdmin = UserAccountId.Create();
    private static readonly UserAccountId ValidDelegatedAdmin = UserAccountId.Create();
    private static readonly ActorId ValidActor = ActorId.Create("admin-001");
    private static readonly DelegationScopeType ValidScopeType = DelegationScopeType.Tenant;
    private static readonly IReadOnlyList<DelegatedAction> ValidActions = new[] { DelegatedAction.CreateUser, DelegatedAction.AssignProfile };
    private static readonly DateTimeOffset ValidFrom = DateTimeOffset.UtcNow;
    private static readonly DateTimeOffset ValidUntil = DateTimeOffset.UtcNow.AddDays(30);

    #region Create

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId, ValidDelegatingAdmin, ValidDelegatedAdmin,
            ValidScopeType, null, ValidActions, ValidFrom, ValidUntil, null, false, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Draft, result.Value.Status);
    }

    [Fact]
    public void Create_WhenSelfDelegation_ReturnsFailure()
    {
        var result = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId, ValidDelegatingAdmin, ValidDelegatingAdmin,
            ValidScopeType, null, ValidActions, ValidFrom, ValidUntil, null, false, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Delegation.SelfDelegationNotAllowed, result.Error);
    }

    [Fact]
    public void Create_WhenValidUntilBeforeValidFrom_ReturnsFailure()
    {
        var result = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId, ValidDelegatingAdmin, ValidDelegatedAdmin,
            ValidScopeType, null, ValidActions, ValidUntil, ValidFrom, null, false, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Delegation.ValidUntilMustBeAfterValidFrom, result.Error);
    }

    [Fact]
    public void Create_WhenNoAllowedActions_ReturnsFailure()
    {
        var result = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId, ValidDelegatingAdmin, ValidDelegatedAdmin,
            ValidScopeType, null, Array.Empty<DelegatedAction>(), ValidFrom, ValidUntil, null, false, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Delegation.AllowedActionsRequired, result.Error);
    }

    [Fact]
    public void Create_RaisesDelegationCreatedEvent()
    {
        var result = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId, ValidDelegatingAdmin, ValidDelegatedAdmin,
            ValidScopeType, null, ValidActions, ValidFrom, ValidUntil, null, false, ValidActor);

        Assert.True(result.IsSuccess);
        var events = result.Value.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Single(events);
        Assert.IsType<DelegationCreatedEvent>(events[0]);
    }

    #endregion

    #region Activate

    [Fact]
    public void Activate_WhenDraft_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();

        var result = delegation.Activate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Active, delegation.Status);
    }

    [Fact]
    public void Activate_WhenPendingApproval_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();
        delegation.SubmitForApproval(Guid.NewGuid(), ValidActor);

        var result = delegation.Activate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Active, delegation.Status);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ReturnsFailure()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);

        var result = delegation.Activate(ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Activate_WhenRevoked_ReturnsFailure()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);
        delegation.Revoke("Test reason", ValidActor);

        var result = delegation.Activate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Delegation.CannotActivateFromCurrentStatus, result.Error);
    }

    [Fact]
    public void Activate_RaisesDelegationActivatedEvent()
    {
        var delegation = CreateValidDelegation();

        delegation.Activate(ValidActor);

        var events = delegation.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is DelegationActivatedEvent);
    }

    [Fact]
    public void Activate_WhenRequiresApproval_IsBlockedFailClosed()
    {
        // G-056: una delegación con requiresApproval=true NO puede activarse directamente.
        // La única vía legítima a Active es SubmitForApproval → Approve.
        var delegation = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId, ValidDelegatingAdmin, ValidDelegatedAdmin,
            ValidScopeType, null, ValidActions, ValidFrom, ValidUntil, null, requiresApproval: true, ValidActor).Value;

        var result = delegation.Activate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Delegation.ApprovalRequired, result.Error);
        Assert.Equal(DelegationStatus.Draft, delegation.Status);
    }

    [Fact]
    public void Activate_WhenRequiresApproval_AndApproved_ReturnsSuccess()
    {
        // G-056: tras la aprobación, la delegación sí queda Active. La compuerta no impide el
        // flujo legítimo, sólo el atajo que la eludía.
        var delegation = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId, ValidDelegatingAdmin, ValidDelegatedAdmin,
            ValidScopeType, null, ValidActions, ValidFrom, ValidUntil, null, requiresApproval: true, ValidActor).Value;

        delegation.SubmitForApproval(Guid.NewGuid(), ValidActor);
        var approveResult = delegation.Approve(ValidActor);

        Assert.True(approveResult.IsSuccess);
        Assert.Equal(DelegationStatus.Active, delegation.Status);
    }

    [Fact]
    public void Activate_WhenRequiresApproval_AndSubmittedButNotApproved_IsBlocked()
    {
        // G-056: estando en PendingApproval, Activate directo sigue vedado mientras persista
        // requiresApproval — sólo Approve puede promover a Active.
        var delegation = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId, ValidDelegatingAdmin, ValidDelegatedAdmin,
            ValidScopeType, null, ValidActions, ValidFrom, ValidUntil, null, requiresApproval: true, ValidActor).Value;

        delegation.SubmitForApproval(Guid.NewGuid(), ValidActor);
        var result = delegation.Activate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Delegation.ApprovalRequired, result.Error);
        Assert.Equal(DelegationStatus.PendingApproval, delegation.Status);
    }

    #endregion

    #region SubmitForApproval

    [Fact]
    public void SubmitForApproval_WhenDraft_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();
        var approvalId = Guid.NewGuid();

        var result = delegation.SubmitForApproval(approvalId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.PendingApproval, delegation.Status);
    }

    [Fact]
    public void SubmitForApproval_WhenNotDraft_ReturnsFailure()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);

        var result = delegation.SubmitForApproval(Guid.NewGuid(), ValidActor);

        Assert.True(result.IsFailure);
    }

    #endregion

    #region Approve

    [Fact]
    public void Approve_WhenPendingApproval_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();
        delegation.SubmitForApproval(Guid.NewGuid(), ValidActor);

        var result = delegation.Approve(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Active, delegation.Status);
    }

    [Fact]
    public void Approve_WhenNotPendingApproval_ReturnsFailure()
    {
        var delegation = CreateValidDelegation();

        var result = delegation.Approve(ValidActor);

        Assert.True(result.IsFailure);
    }

    // G-150 / INV-DEL8 (separación de funciones): el administrador delegante no puede autoaprobar
    // su propia delegación. El aprobador (actorId) que coincide con DelegatingAdminId → falla,
    // el estado NO transita a Active y el motivo es SelfApprovalNotAllowed.
    [Fact]
    public void Approve_WhenApproverIsDelegatingAdmin_ReturnsFailure()
    {
        var delegatingAdminId = Guid.NewGuid();
        var delegation = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId,
            UserAccountId.Load(delegatingAdminId),
            ValidDelegatedAdmin,
            ValidScopeType, null, ValidActions, ValidFrom, ValidUntil, null, true,
            ActorId.Create(delegatingAdminId.ToString())).Value;
        delegation.SubmitForApproval(Guid.NewGuid(), ActorId.Create(delegatingAdminId.ToString()));

        // El mismo administrador delegante intenta aprobar → SoD lo impide.
        var result = delegation.Approve(ActorId.Create(delegatingAdminId.ToString()));

        Assert.True(result.IsFailure);
        Assert.Contains("self_approval_not_allowed", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DelegationStatus.PendingApproval, delegation.Status);
    }

    // Control positivo de la SoD: un aprobador DISTINTO del delegante sí puede aprobar.
    [Fact]
    public void Approve_WhenApproverIsDifferentFromDelegatingAdmin_ReturnsSuccess()
    {
        var delegatingAdminId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var delegation = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId,
            UserAccountId.Load(delegatingAdminId),
            ValidDelegatedAdmin,
            ValidScopeType, null, ValidActions, ValidFrom, ValidUntil, null, true,
            ActorId.Create(delegatingAdminId.ToString())).Value;
        delegation.SubmitForApproval(Guid.NewGuid(), ActorId.Create(delegatingAdminId.ToString()));

        var result = delegation.Approve(ActorId.Create(approverId.ToString()));

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Active, delegation.Status);
    }

    #endregion

    #region Reject

    [Fact]
    public void Reject_WhenPendingApproval_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();
        delegation.SubmitForApproval(Guid.NewGuid(), ValidActor);

        var result = delegation.Reject("Not approved", ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Rejected, delegation.Status);
    }

    [Fact]
    public void Reject_WithEmptyReason_ReturnsFailure()
    {
        var delegation = CreateValidDelegation();
        delegation.SubmitForApproval(Guid.NewGuid(), ValidActor);

        var result = delegation.Reject("", ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Delegation.RevocationReasonRequired, result.Error);
    }

    [Fact]
    public void Reject_RaisesDelegationRejectedEvent()
    {
        var delegation = CreateValidDelegation();
        delegation.SubmitForApproval(Guid.NewGuid(), ValidActor);

        delegation.Reject("Not approved", ValidActor);

        var events = delegation.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is DelegationRejectedEvent);
    }

    #endregion

    #region Revoke

    [Fact]
    public void Revoke_WhenActive_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);

        var result = delegation.Revoke("No longer needed", ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Revoked, delegation.Status);
        Assert.NotNull(delegation.RevokedAt);
        Assert.Equal("No longer needed", delegation.RevocationReason);
    }

    [Fact]
    public void Revoke_WhenPendingApproval_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();
        delegation.SubmitForApproval(Guid.NewGuid(), ValidActor);

        var result = delegation.Revoke("Cancelled", ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Revoked, delegation.Status);
    }

    [Fact]
    public void Revoke_WhenDraft_ReturnsFailure()
    {
        var delegation = CreateValidDelegation();

        var result = delegation.Revoke("Cancelled", ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Delegation.CannotRevokeFromCurrentStatus, result.Error);
    }

    [Fact]
    public void Revoke_WithEmptyReason_ReturnsFailure()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);

        var result = delegation.Revoke("", ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Revoke_RaisesDelegationRevokedEvent()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);

        delegation.Revoke("No longer needed", ValidActor);

        var events = delegation.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is DelegationRevokedEvent);
    }

    #endregion

    #region Expire

    [Fact]
    public void Expire_WhenActive_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);

        var result = delegation.Expire(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Expired, delegation.Status);
    }

    [Fact]
    public void Expire_WhenNotActive_ReturnsFailure()
    {
        var delegation = CreateValidDelegation();

        var result = delegation.Expire(ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Expire_RaisesDelegationExpiredEvent()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);

        delegation.Expire(ValidActor);

        var events = delegation.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is DelegationExpiredEvent);
    }

    #endregion

    #region Complete

    [Fact]
    public void Complete_WhenActive_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);

        var result = delegation.Complete(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Completed, delegation.Status);
    }

    [Fact]
    public void Complete_WhenNotActive_ReturnsFailure()
    {
        var delegation = CreateValidDelegation();

        var result = delegation.Complete(ValidActor);

        Assert.True(result.IsFailure);
    }

    #endregion

    #region Archive

    [Fact]
    public void Archive_WhenRevoked_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);
        delegation.Revoke("Done", ValidActor);

        var result = delegation.Archive(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Archived, delegation.Status);
    }

    [Fact]
    public void Archive_WhenExpired_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);
        delegation.Expire(ValidActor);

        var result = delegation.Archive(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Archived, delegation.Status);
    }

    [Fact]
    public void Archive_WhenCompleted_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);
        delegation.Complete(ValidActor);

        var result = delegation.Archive(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Archived, delegation.Status);
    }

    [Fact]
    public void Archive_WhenRejected_ReturnsSuccess()
    {
        var delegation = CreateValidDelegation();
        delegation.SubmitForApproval(Guid.NewGuid(), ValidActor);
        delegation.Reject("No", ValidActor);

        var result = delegation.Archive(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Archived, delegation.Status);
    }

    [Fact]
    public void Archive_WhenActive_ReturnsFailure()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);

        var result = delegation.Archive(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Delegation.CannotArchiveFromCurrentStatus, result.Error);
    }

    [Fact]
    public void Archive_RaisesDelegationArchivedEvent()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);
        delegation.Revoke("Done", ValidActor);

        delegation.Archive(ValidActor);

        var events = delegation.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is DelegationArchivedEvent);
    }

    #endregion

    #region CanExecuteAction

    [Fact]
    public void CanExecuteAction_WhenActiveAndActionAllowed_ReturnsTrue()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);

        var result = delegation.CanExecuteAction(DelegatedAction.CreateUser, null);

        Assert.True(result);
    }

    [Fact]
    public void CanExecuteAction_WhenActiveButActionNotAllowed_ReturnsFalse()
    {
        var delegation = CreateValidDelegation();
        delegation.Activate(ValidActor);

        var result = delegation.CanExecuteAction(DelegatedAction.RevokeMfa, null);

        Assert.False(result);
    }

    [Fact]
    public void CanExecuteAction_WhenNotActive_ReturnsFalse()
    {
        var delegation = CreateValidDelegation();

        var result = delegation.CanExecuteAction(DelegatedAction.CreateUser, null);

        Assert.False(result);
    }

    [Fact]
    public void CanExecuteAction_WhenScopeIdMismatch_ReturnsFalse()
    {
        var scopeId = Guid.NewGuid();
        var delegation = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId, ValidDelegatingAdmin, ValidDelegatedAdmin,
            ValidScopeType, scopeId, ValidActions, ValidFrom, ValidUntil, null, false, ValidActor).Value;
        delegation.Activate(ValidActor);

        var result = delegation.CanExecuteAction(DelegatedAction.CreateUser, Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public void CanExecuteAction_WhenScopeIdMatches_ReturnsTrue()
    {
        var scopeId = Guid.NewGuid();
        var delegation = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId, ValidDelegatingAdmin, ValidDelegatedAdmin,
            ValidScopeType, scopeId, ValidActions, ValidFrom, ValidUntil, null, false, ValidActor).Value;
        delegation.Activate(ValidActor);

        var result = delegation.CanExecuteAction(DelegatedAction.CreateUser, scopeId);

        Assert.True(result);
    }

    #endregion

    #region DelegatedAction Enumeration Tests

    [Fact]
    public void DelegatedAction_HasExpectedActions()
    {
        Assert.Equal(1, DelegatedAction.CreateUser.Id);
        Assert.Equal(2, DelegatedAction.BlockUser.Id);
        Assert.Equal(3, DelegatedAction.AssignProfile.Id);
        Assert.Equal(4, DelegatedAction.ResetPassword.Id);
        Assert.Equal(5, DelegatedAction.RevokeMfa.Id);
        Assert.Equal(6, DelegatedAction.ManageProfilePermissions.Id);
    }

    #endregion

    #region DelegationScopeType Enumeration Tests

    [Fact]
    public void DelegationScopeType_HasExpectedTypes()
    {
        Assert.Equal(1, DelegationScopeType.Tenant.Id);
        Assert.Equal(2, DelegationScopeType.Organization.Id);
        Assert.Equal(3, DelegationScopeType.Department.Id);
        Assert.Equal(4, DelegationScopeType.System.Id);
        Assert.Equal(5, DelegationScopeType.Team.Id);
    }

    #endregion

    #region DelegationStatus Enumeration Tests

    [Fact]
    public void DelegationStatus_HasExpectedStatuses()
    {
        Assert.Equal(1, DelegationStatus.Draft.Id);
        Assert.Equal(2, DelegationStatus.PendingApproval.Id);
        Assert.Equal(3, DelegationStatus.Active.Id);
        Assert.Equal(4, DelegationStatus.Revoked.Id);
        Assert.Equal(5, DelegationStatus.Expired.Id);
        Assert.Equal(6, DelegationStatus.Completed.Id);
        Assert.Equal(7, DelegationStatus.Rejected.Id);
        Assert.Equal(8, DelegationStatus.Archived.Id);
    }

    #endregion

    private static Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation CreateValidDelegation()
    {
        return Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            ValidTenantId, ValidDelegatingAdmin, ValidDelegatedAdmin,
            ValidScopeType, null, ValidActions, ValidFrom, ValidUntil, null, false, ValidActor).Value;
    }
}
