namespace Evolith.Ums.Application.Test.Approvals.ApprovalRequest;

using System;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Approvals.ApprovalRequest.Commands;
using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;
using Evolith.Ums.Application.Approvals.ApprovalRequest.Services;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.ApprovalRequest;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Profile;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Identity.UserManagementDelegation;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Moq;
using Xunit;
using ApprovalWorkflowAggregate = Evolith.Ums.Domain.Approvals.ApprovalWorkflow.ApprovalWorkflow;
using RoleAggregate = Evolith.Ums.Domain.Authorization.Role.Role;
using UserDocumentAggregate = Evolith.Ums.Domain.Approvals.UserDocument.UserDocument;

public class ApprovalRequestCommandHandlerTests
{
    private readonly Mock<IApprovalRequestRepository>             _repo                   = new();
    private readonly Mock<IProfileRepository>                    _profileRepo            = new();
    private readonly Mock<IApprovalWorkflowRepository>            _workflowRepo           = new();
    private readonly Mock<IUserDocumentRepository>                _userDocumentRepo       = new();
    private readonly Mock<IApprovalRequestCreationPolicyResolver> _creationPolicyResolver = new();
    private readonly Mock<IUserAccountRepository>                 _userAccountRepo        = new();
    private readonly Mock<ITenantRepository>                      _tenantRepo             = new();
    private readonly Mock<IUserManagementDelegationRepository>    _delegationRepo         = new();
    private readonly Mock<ITenantScopePolicy>                     _tenantScopePolicy      = new();
    private readonly Mock<IUnitOfWorkScope>                       _unitOfWorkScope        = new();
    private readonly Mock<ITransactionScope>                      _transactionScope       = new();
    private readonly Mock<INotificationService>                   _notifications          = new();
    private readonly Mock<IRoleRepository>                        _roleRepo               = new();
    private readonly Mock<IUnitOfWork>                            _approvalUow            = new();
    private readonly Mock<IUnitOfWork>                            _profileUow             = new();
    private readonly Mock<IUserContext>                           _ctx                    = new();

    private static readonly Guid          TenantId     = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly SystemSuiteId ValidSystemId = SystemSuiteId.Load(Guid.NewGuid());
    private static readonly RoleId        ValidRoleId   = RoleId.Load(Guid.NewGuid());

    public ApprovalRequestCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_approvalUow.Object);
        _profileRepo.Setup(r => r.UnitOfWork).Returns(_profileUow.Object);
        _roleRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRole());
        _approvalUow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _profileUow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _tenantScopePolicy.Setup(p => p.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _unitOfWorkScope.Setup(u => u.BeginAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_transactionScope.Object);
        _transactionScope.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _transactionScope.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        // G-117: el fake ejecuta la operación (como la ExecutionStrategy real hace begin+commit); sin
        // esto Moq devolvería una tarea vacía y los saves del bloque transaccional no correrían.
        _unitOfWorkScope
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> op, CancellationToken ct) => op(ct));
        _ctx.Setup(u => u.UserId).Returns("user-001");

        // Por defecto el checklist de documentos requeridos se satisface: workflow sin documentos
        // obligatorios y sin documentos del usuario. Los tests de G-051 F4 sobrescriben esto.
        _workflowRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeWorkflow());
        _userDocumentRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserDocumentAggregate>());
    }

    // G-119 (SoD): el creador es "requester-001", distinto del aprobador ("user-001", _ctx.UserId),
    // para que el happy-path de approve no viole la segregación de deberes.
    private static ApprovalRequest MakeApprovalRequest(ProfileId? profileId = null, string createdBy = "requester-001") =>
        ApprovalRequest.Create(
            ApprovalWorkflowId.Load(Guid.NewGuid()),
            UserId.Load(Guid.NewGuid()),
            profileId,
            ValidSystemId, null, ValidRoleId, null,
            ActorId.Create(createdBy)).Value;

    private static ApprovalWorkflowAggregate MakeWorkflow(bool requiresApproval = true, UserCategory? category = null) =>
        ApprovalWorkflowAggregate.Create(
            Domain.Kernel.ValueObjects.TenantId.Load(Guid.NewGuid()),
            Code.Create($"wf-{Guid.NewGuid():N}"),
            Name.Create("Workflow"),
            Description.Create("Workflow description"),
            category ?? UserCategory.Internal,
            requiresApproval,
            null,
            ActorId.Create("user-001"),
            requiredDocumentCount: requiresApproval ? 1 : 0).Value;

    private static UserAccount MakeExternalUser(UserCategory? category = null)
    {
        var user = UserAccount.Create(
            Domain.Kernel.ValueObjects.TenantId.Load(TenantId),
            Email.Create("external@partner.com"),
            category ?? UserCategory.External,
            null, null,
            ActorId.Create("sys")).Value;
        user.Activate(ActorId.Create("sys"));
        return user;
    }

    private static UserAccount MakeInternalUser()
    {
        var user = UserAccount.Create(
            Domain.Kernel.ValueObjects.TenantId.Load(TenantId),
            Email.Create("internal@company.com"),
            UserCategory.Internal,
            null, null,
            ActorId.Create("sys")).Value;
        user.Activate(ActorId.Create("sys"));
        return user;
    }

    private static Domain.Identity.Tenant.Tenant MakeTenant() =>
        Domain.Identity.Tenant.Tenant.Create(
            Code.Create("CORP"), Name.Create("Corp Inc"),
            OrganizationType.INTERNAL, ActorId.Create("sys"),
            tenantId: Domain.Kernel.ValueObjects.TenantId.Load(TenantId)).Value;

    private CreateApprovalRequestCommandHandler CreateHandler() =>
        new(_repo.Object, _workflowRepo.Object, _creationPolicyResolver.Object, _userAccountRepo.Object, _ctx.Object);

    private ApproveRequestCommandHandler CreateApproveHandler() =>
        new(_repo.Object, _profileRepo.Object, _workflowRepo.Object, _userDocumentRepo.Object, _userAccountRepo.Object, _tenantRepo.Object, _delegationRepo.Object, _tenantScopePolicy.Object, _unitOfWorkScope.Object, _notifications.Object, _roleRepo.Object, _ctx.Object);

    // G-160: por defecto el repo de roles resuelve un rol válido, para que las aprobaciones happy-path
    // no fallen por la nueva guarda de existencia del rol concedido.
    private static RoleAggregate MakeRole() =>
        RoleAggregate.Create(
            Domain.Kernel.ValueObjects.TenantId.Load(TenantId),
            SystemSuiteId.Load(Guid.NewGuid()),
            Code.Create("ROLE_TEST"), Name.Create("Rol de Prueba"), Description.Create("rol"),
            null, 0, 0, ActorId.Create("sys")).Value;

    private static ApprovalWorkflowAggregate MakeWorkflowRequiring(DocumentTypeId documentTypeId, bool isMandatory = true)
    {
        var workflow = MakeWorkflow();
        workflow.AddRequiredDocument(documentTypeId, isMandatory, ActorId.Create("user-001"));
        return workflow;
    }

    private static UserDocumentAggregate MakeValidDocument(DocumentTypeId documentTypeId)
    {
        var document = UserDocumentAggregate.Upload(
            UserId.Load(Guid.NewGuid()),
            documentTypeId,
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DocumentCriticity.High,
            TextValueObject.Create("/storage/doc.pdf"),
            "checksum-001",
            ActorId.Create("user-001")).Value;
        document.Validate(ActorId.Create("user-001"));
        return document;
    }

    private static UserDocumentAggregate MakePendingDocument(DocumentTypeId documentTypeId) =>
        UserDocumentAggregate.Upload(
            UserId.Load(Guid.NewGuid()),
            documentTypeId,
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DocumentCriticity.High,
            TextValueObject.Create("/storage/doc.pdf"),
            "checksum-002",
            ActorId.Create("user-001")).Value;

    private RejectRequestCommandHandler CreateRejectHandler() =>
        new(_repo.Object, _userAccountRepo.Object, _tenantRepo.Object, _notifications.Object, _ctx.Object);

    // =========================================================================
    #region CreateApprovalRequestCommandHandler
    // =========================================================================

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccess()
    {
        var cmd = new CreateApprovalRequestCommand(
            WorkflowId: Guid.NewGuid(),
            TargetUserId: Guid.NewGuid(),
            TargetProfileId: Guid.NewGuid(),
            RequestedSystemId: Guid.NewGuid(),
            RequestedBranchId: null,
            RequestedRoleId: Guid.NewGuid(),
            Justification: null);

        var workflow       = MakeWorkflow();
        var internalUser   = MakeInternalUser();
        var createdRequest = MakeApprovalRequest();

        _workflowRepo.Setup(r => r.GetByIdAsync(cmd.WorkflowId, It.IsAny<CancellationToken>())).ReturnsAsync(workflow);
        _userAccountRepo.Setup(r => r.GetByIdAsync(cmd.TargetUserId, It.IsAny<CancellationToken>())).ReturnsAsync(internalUser);
        _repo.Setup(r => r.ExistsPendingForScopeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _creationPolicyResolver.Setup(r => r.Create(
                workflow,
                It.IsAny<UserId>(),
                It.IsAny<ProfileId?>(),
                It.IsAny<SystemSuiteId>(),
                It.IsAny<BranchId?>(),
                It.IsAny<RoleId>(),
                It.IsAny<string?>(),
                It.IsAny<ActorId>()))
            .Returns(Result<ApprovalRequest>.Success(createdRequest));

        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.ApprovalRequestId);
        _repo.Verify(r => r.AddAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _approvalUow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new CreateApprovalRequestCommand(
            WorkflowId: Guid.NewGuid(), TargetUserId: Guid.NewGuid(),
            TargetProfileId: Guid.NewGuid(), RequestedSystemId: Guid.NewGuid(),
            RequestedBranchId: null, RequestedRoleId: Guid.NewGuid(), Justification: null);

        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenWorkflowNotFound_ReturnsFailure()
    {
        var cmd = new CreateApprovalRequestCommand(
            WorkflowId: Guid.NewGuid(), TargetUserId: Guid.NewGuid(),
            TargetProfileId: Guid.NewGuid(), RequestedSystemId: Guid.NewGuid(),
            RequestedBranchId: null, RequestedRoleId: Guid.NewGuid(), Justification: null);

        _workflowRepo.Setup(r => r.GetByIdAsync(cmd.WorkflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApprovalWorkflowAggregate?)null);

        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("approval workflow not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenTargetUserNotFound_ReturnsFailure()
    {
        var cmd = new CreateApprovalRequestCommand(
            WorkflowId: Guid.NewGuid(), TargetUserId: Guid.NewGuid(),
            TargetProfileId: Guid.NewGuid(), RequestedSystemId: Guid.NewGuid(),
            RequestedBranchId: null, RequestedRoleId: Guid.NewGuid(), Justification: null);

        _workflowRepo.Setup(r => r.GetByIdAsync(cmd.WorkflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeWorkflow());
        _userAccountRepo.Setup(r => r.GetByIdAsync(cmd.TargetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAccount?)null);

        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenInternalWorkflowUsedByExternalUser_ReturnsPrivilegeEscalationError()
    {
        var externalUser   = MakeExternalUser();
        var internalWorkflow = MakeWorkflow(category: UserCategory.Internal);

        var cmd = new CreateApprovalRequestCommand(
            WorkflowId: Guid.NewGuid(), TargetUserId: Guid.NewGuid(),
            TargetProfileId: Guid.NewGuid(), RequestedSystemId: Guid.NewGuid(),
            RequestedBranchId: null, RequestedRoleId: Guid.NewGuid(), Justification: "B2B access");

        _workflowRepo.Setup(r => r.GetByIdAsync(cmd.WorkflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(internalWorkflow);
        _userAccountRepo.Setup(r => r.GetByIdAsync(cmd.TargetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalUser);

        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.WorkflowNotAllowedForUserCategory, result.Error);
    }

    [Fact]
    public async Task Create_WhenInternalWorkflowUsedByB2BUser_ReturnsPrivilegeEscalationError()
    {
        var b2bUser          = MakeExternalUser(UserCategory.B2B);
        var internalWorkflow = MakeWorkflow(category: UserCategory.Internal);

        var cmd = new CreateApprovalRequestCommand(
            WorkflowId: Guid.NewGuid(), TargetUserId: Guid.NewGuid(),
            TargetProfileId: null, RequestedSystemId: Guid.NewGuid(),
            RequestedBranchId: null, RequestedRoleId: Guid.NewGuid(), Justification: "B2B");

        _workflowRepo.Setup(r => r.GetByIdAsync(cmd.WorkflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(internalWorkflow);
        _userAccountRepo.Setup(r => r.GetByIdAsync(cmd.TargetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(b2bUser);

        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.WorkflowNotAllowedForUserCategory, result.Error);
    }

    [Fact]
    public async Task Create_WhenExternalWorkflowUsedByExternalUser_Succeeds()
    {
        var externalUser     = MakeExternalUser();
        var externalWorkflow = MakeWorkflow(category: UserCategory.External);
        var createdRequest   = MakeApprovalRequest();

        var cmd = new CreateApprovalRequestCommand(
            WorkflowId: Guid.NewGuid(), TargetUserId: Guid.NewGuid(),
            TargetProfileId: null, RequestedSystemId: Guid.NewGuid(),
            RequestedBranchId: null, RequestedRoleId: Guid.NewGuid(), Justification: "B2B");

        _workflowRepo.Setup(r => r.GetByIdAsync(cmd.WorkflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalWorkflow);
        _userAccountRepo.Setup(r => r.GetByIdAsync(cmd.TargetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalUser);
        _repo.Setup(r => r.ExistsPendingForScopeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _creationPolicyResolver.Setup(r => r.Create(
                externalWorkflow,
                It.IsAny<UserId>(), It.IsAny<ProfileId?>(), It.IsAny<SystemSuiteId>(),
                It.IsAny<BranchId?>(), It.IsAny<RoleId>(), It.IsAny<string?>(), It.IsAny<ActorId>()))
            .Returns(Result<ApprovalRequest>.Success(createdRequest));

        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    #endregion

    // =========================================================================
    #region ApproveRequestCommandHandler
    // =========================================================================

    [Fact]
    public async Task Approve_WithValidCommand_ReturnsSuccess()
    {
        var req  = MakeApprovalRequest();
        var user = MakeExternalUser();
        var tenant = MakeTenant();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _profileRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Profile>());

        var result = await CreateApproveHandler().Handle(
            new ApproveRequestCommand(req.Props.Id.GetValue(), ValidRoleId.GetValue()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Approved, req.Status);
        Assert.NotNull(req.TargetProfileId);
        _profileRepo.Verify(r => r.AddAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.UpdateAsync(req, It.IsAny<CancellationToken>()), Times.Once);
        _approvalUow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _profileUow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // G-119 (SoD): el creador de la solicitud NO puede aprobarla (self-approval prohibido).
    [Fact]
    public async Task Approve_WhenApproverIsCreator_ReturnsFailure_SelfApproval()
    {
        // createdBy == aprobador (_ctx.UserId="user-001") → debe rechazarse.
        var req    = MakeApprovalRequest(createdBy: "user-001");
        var user   = MakeExternalUser();
        var tenant = MakeTenant();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _profileRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Profile>());

        var result = await CreateApproveHandler().Handle(
            new ApproveRequestCommand(req.Props.Id.GetValue(), ValidRoleId.GetValue()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.SelfApprovalNotAllowed, result.Error);
        Assert.Equal(ApprovalStatus.Pending, req.Status);
    }

    [Fact]
    public async Task Approve_WithValidCommand_SendsApprovalNotificationToApplicant()
    {
        var req    = MakeApprovalRequest();
        var user   = MakeExternalUser();
        var tenant = MakeTenant();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _profileRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Profile>());

        await CreateApproveHandler().Handle(
            new ApproveRequestCommand(req.Props.Id.GetValue(), ValidRoleId.GetValue()),
            CancellationToken.None);

        _notifications.Verify(
            n => n.SendAsync(
                It.Is<UmsNotification>(n => n.Recipient == "external@partner.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Approve_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((ApprovalRequest?)null);

        var result = await CreateApproveHandler().Handle(
            new ApproveRequestCommand(Guid.NewGuid(), ValidRoleId.GetValue()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("approval request not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Approve_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var result = await CreateApproveHandler().Handle(
            new ApproveRequestCommand(Guid.NewGuid(), ValidRoleId.GetValue()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Approve_WhenNotPending_ReturnsFailure()
    {
        var req = MakeApprovalRequest();
        req.Approve(ActorId.Create("user-001"), ValidRoleId);
        var user = MakeExternalUser();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _profileRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Profile>());

        var result = await CreateApproveHandler().Handle(
            new ApproveRequestCommand(req.Props.Id.GetValue(), ValidRoleId.GetValue()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Approve_WithDelegatedBranchManagerScope_ReturnsSuccess()
    {
        var req       = MakeApprovalRequest();
        var user      = MakeExternalUser();
        var tenant    = MakeTenant();
        var managerId = Guid.NewGuid();

        _ctx.Setup(u => u.UserId).Returns(managerId.ToString());
        _tenantScopePolicy.Setup(p => p.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("AUTH_015: Tenant is not marked as management owner."));

        var delegation = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            Domain.Kernel.ValueObjects.TenantId.Load(TenantId),
            UserAccountId.Load(Guid.NewGuid()),
            UserAccountId.Load(managerId),
            DelegationScopeType.Tenant,
            null,
            new[] { DelegatedAction.AssignProfile },
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            null,
            false,
            ActorId.Create("sys")).Value;
        delegation.Activate(ActorId.Create("sys"));

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _delegationRepo.Setup(r => r.GetActiveAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { delegation });
        _profileRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Profile>());

        var result = await CreateApproveHandler().Handle(
            new ApproveRequestCommand(req.Props.Id.GetValue(), ValidRoleId.GetValue()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(req.TargetProfileId);
    }

    // ---- G-051 F4: exigencia cross-agregado del checklist de documentos requeridos ----

    [Fact]
    public async Task Approve_WhenMandatoryDocumentMissing_ReturnsFailureAndKeepsPending()
    {
        var docType = DocumentTypeId.Load(Guid.NewGuid());
        var req     = MakeApprovalRequest();
        var user    = MakeExternalUser();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _workflowRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeWorkflowRequiring(docType));
        // El usuario no tiene ningun documento: checklist obligatorio incompleto.
        _userDocumentRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserDocumentAggregate>());

        var result = await CreateApproveHandler().Handle(
            new ApproveRequestCommand(req.Props.Id.GetValue(), ValidRoleId.GetValue()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RequiredDocumentsIncomplete, result.Error);
        Assert.Equal(ApprovalStatus.Pending, req.Status);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Approve_WhenMandatoryDocumentValid_ReturnsSuccess()
    {
        var docType = DocumentTypeId.Load(Guid.NewGuid());
        var req     = MakeApprovalRequest();
        var user    = MakeExternalUser();
        var tenant  = MakeTenant();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _profileRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Profile>());
        _workflowRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeWorkflowRequiring(docType));
        _userDocumentRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeValidDocument(docType) });

        var result = await CreateApproveHandler().Handle(
            new ApproveRequestCommand(req.Props.Id.GetValue(), ValidRoleId.GetValue()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Approved, req.Status);
    }

    [Fact]
    public async Task Approve_WhenMandatoryDocumentNotYetValid_ReturnsFailure()
    {
        var docType = DocumentTypeId.Load(Guid.NewGuid());
        var req     = MakeApprovalRequest();
        var user    = MakeExternalUser();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _workflowRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeWorkflowRequiring(docType));
        // Documento subido pero aun en PENDING_REVIEW: no cuenta como cumplimiento.
        _userDocumentRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakePendingDocument(docType) });

        var result = await CreateApproveHandler().Handle(
            new ApproveRequestCommand(req.Props.Id.GetValue(), ValidRoleId.GetValue()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RequiredDocumentsIncomplete, result.Error);
        Assert.Equal(ApprovalStatus.Pending, req.Status);
    }

    [Fact]
    public async Task Approve_WhenNonMandatoryDocumentMissing_ReturnsSuccess()
    {
        var docType = DocumentTypeId.Load(Guid.NewGuid());
        var req     = MakeApprovalRequest();
        var user    = MakeExternalUser();
        var tenant  = MakeTenant();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _profileRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Profile>());
        // Documento requerido pero NO obligatorio: su ausencia no bloquea la aprobacion.
        _workflowRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeWorkflowRequiring(docType, isMandatory: false));
        _userDocumentRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserDocumentAggregate>());

        var result = await CreateApproveHandler().Handle(
            new ApproveRequestCommand(req.Props.Id.GetValue(), ValidRoleId.GetValue()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Approved, req.Status);
    }

    [Fact]
    public async Task Approve_WhenWorkflowNotResolvable_FailsClosed()
    {
        var req  = MakeApprovalRequest();
        var user = MakeExternalUser();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _workflowRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApprovalWorkflowAggregate?)null);

        var result = await CreateApproveHandler().Handle(
            new ApproveRequestCommand(req.Props.Id.GetValue(), ValidRoleId.GetValue()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RequiredDocumentsIncomplete, result.Error);
        Assert.Equal(ApprovalStatus.Pending, req.Status);
    }

    #endregion

    // =========================================================================
    #region RejectRequestCommandHandler
    // =========================================================================

    [Fact]
    public async Task Reject_WithValidCommand_ReturnsSuccess()
    {
        var req    = MakeApprovalRequest();
        var user   = MakeExternalUser();
        var tenant = MakeTenant();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);

        var result = await CreateRejectHandler().Handle(
            new RejectRequestCommand(req.Props.Id.GetValue()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Rejected, req.Status);
        _repo.Verify(r => r.UpdateAsync(req, It.IsAny<CancellationToken>()), Times.Once);
        _approvalUow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reject_WithValidCommand_SendsDenialNotificationToApplicant()
    {
        var req    = MakeApprovalRequest();
        var user   = MakeExternalUser();
        var tenant = MakeTenant();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);

        await CreateRejectHandler().Handle(
            new RejectRequestCommand(req.Props.Id.GetValue(), "Not eligible"),
            CancellationToken.None);

        _notifications.Verify(
            n => n.SendAsync(
                It.Is<UmsNotification>(n => n.Recipient == "external@partner.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Reject_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((ApprovalRequest?)null);

        var result = await CreateRejectHandler().Handle(
            new RejectRequestCommand(Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("approval request not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reject_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var result = await CreateRejectHandler().Handle(
            new RejectRequestCommand(Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
