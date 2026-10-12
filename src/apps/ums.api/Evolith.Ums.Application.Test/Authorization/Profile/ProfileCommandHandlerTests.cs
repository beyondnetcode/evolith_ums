namespace Evolith.Ums.Application.Test.Authorization.Profile;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Authorization.Profile.Commands;
using Evolith.Ums.Domain.Authorization.Profile;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

public class ProfileCommandHandlerTests
{
    private readonly Mock<IProfileRepository> _repo = new();
    private readonly Mock<IPermissionTemplateRepository> _templateRepo = new();
    private readonly Mock<ITemplateAssignmentRuleRepository> _ruleRepo = new();
    private readonly Mock<IUserAccountRepository> _userAccountRepo = new();
    private readonly Mock<IRoleRepository> _roleRepo = new();
    private readonly Mock<IUnitOfWork>         _uow  = new();
    private readonly Mock<IUserContext>        _ctx  = new();
    private readonly Mock<ITenantScopePolicy>  _scopePolicy = new();
    private readonly Mock<IUserManagementDelegationAccessService> _delegationAccess = new();

    private static readonly IReadOnlyList<Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule> NoRules
        = new List<Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule>();

    public ProfileCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _scopePolicy.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    private void SetupNoMatchingRules() =>
        _ruleRepo.Setup(r => r.GetActiveByTenantAndRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NoRules);

    private CreateProfileCommandHandler MakeCreateHandler()
        => new(_repo.Object, _templateRepo.Object, _ruleRepo.Object, _userAccountRepo.Object, _roleRepo.Object, _ctx.Object, _scopePolicy.Object);

    private static Profile MakeProfile()
    {
        return Profile.Create(
            TenantId.Load(Guid.NewGuid()),
            UserId.Load(Guid.NewGuid()),
            RoleId.Load(Guid.NewGuid()),
            null,
            ActorId.Create("user-001")).Value;
    }

    private static Evolith.Ums.Domain.Identity.UserAccount.UserAccount MakeUser(Guid tenantId, Guid userId)
    {
        return Evolith.Ums.Domain.Identity.UserAccount.UserAccount.Create(
            TenantId.Load(tenantId),
            Email.Create($"user.{userId:N}@test.local"),
            UserCategory.Internal,
            null,
            null,
            ActorId.Create("user-001"),
            null,
            UserAccountId.Load(userId)).Value;
    }

    private static Evolith.Ums.Domain.Authorization.Role.Role MakeRole(Guid tenantId, Guid roleId)
    {
        return Evolith.Ums.Domain.Authorization.Role.Role.Create(
            TenantId.Load(tenantId),
            SystemSuiteId.Load(Guid.NewGuid()),
            Code.Create($"ROLE_{roleId:N}"[..12]),
            Name.Create("Role"),
            Description.Create(""),
            null,
            0,
            1,
            ActorId.Create("user-001")).Value;
    }

    private void SetupValidProfileReferences(Guid tenantId, Guid userId, Guid roleId)
    {
        _userAccountRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeUser(tenantId, userId));
        _roleRepo.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRole(tenantId, roleId));
    }

    // =========================================================================
    #region CreateProfileCommandHandler
    // =========================================================================

    /// <summary>
    /// G-215 — un perfil por (usuario, rol, sucursal) activo.
    ///
    /// <para>Sin esta guarda, cada llamada con los mismos datos creaba otro perfil. Un
    /// aprovisionamiento reejecutado dejaba al usuario con dos perfiles del mismo rol —uno con
    /// concesiones y otro vacío, porque la plantilla se asigna a uno solo— y el selector se los
    /// ofrecía indistinguibles. Entrar por el vacío es entrar sin permisos, con todos los HTTP en
    /// 2xx.</para>
    /// </summary>
    [Fact]
    public async Task Create_CuandoYaExisteUnPerfilDelMismoRol_Falla()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        SetupNoMatchingRules();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _userAccountRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeUser(tenantId, userId));
        _roleRepo.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRole(tenantId, roleId));

        var yaExiste = Profile.Create(
            TenantId.Load(tenantId), UserId.Load(userId), RoleId.Load(roleId), null,
            ActorId.Create("user-001")).Value;
        _repo.Setup(r => r.GetActiveByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([yaExiste]);

        var result = await MakeCreateHandler().Handle(
            new CreateProfileCommand(tenantId, userId, roleId, BranchId: null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("profile_already_exists_for_role", result.Error);
        _repo.Verify(r => r.AddAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        SetupNoMatchingRules();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _userAccountRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeUser(tenantId, userId));
        _roleRepo.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRole(tenantId, roleId));

        var cmd = new CreateProfileCommand(
            TenantId: tenantId,
            UserId: userId,
            RoleId: roleId,
            BranchId: null);

        var handler = MakeCreateHandler();
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.ProfileId);
        _repo.Verify(r => r.AddAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");
        SetupNoMatchingRules();

        var cmd = new CreateProfileCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            BranchId: null);

        var handler = MakeCreateHandler();
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenTenantIsNotManagementOwner_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _scopePolicy.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("AUTH_015: Tenant is not marked as management owner."));
        SetupNoMatchingRules();

        var cmd = new CreateProfileCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            BranchId: null);

        var handler = MakeCreateHandler();
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("management owner", result.Error, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenUserAccountDoesNotExist_ReturnsFailure()
    {
        SetupNoMatchingRules();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _userAccountRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Evolith.Ums.Domain.Identity.UserAccount.UserAccount?)null);

        var cmd = new CreateProfileCommand(tenantId, userId, roleId, null);

        var result = await MakeCreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("User account not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenRoleDoesNotExist_ReturnsFailure()
    {
        SetupNoMatchingRules();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _userAccountRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeUser(tenantId, userId));
        _roleRepo.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Evolith.Ums.Domain.Authorization.Role.Role?)null);

        var cmd = new CreateProfileCommand(tenantId, userId, roleId, null);

        var result = await MakeCreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Role not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region ActivateProfileCommandHandler
    // =========================================================================

    [Fact]
    public async Task Activate_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var profile = MakeProfile();
        // Born Active, let's Deactivate it first so we can Activate it again
        profile.Deactivate(ActorId.Create("user-001"));

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(profile);

        var cmd = new ActivateProfileCommand(Guid.NewGuid());
        var handler = new ActivateProfileCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(profile.IsActive);
        _repo.Verify(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Activate_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Profile?)null);

        var cmd = new ActivateProfileCommand(Guid.NewGuid());
        var handler = new ActivateProfileCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Activate_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new ActivateProfileCommand(Guid.NewGuid());
        var handler = new ActivateProfileCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Activate_WhenAlreadyActive_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var profile = MakeProfile(); // already active

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(profile);

        var cmd = new ActivateProfileCommand(Guid.NewGuid());
        var handler = new ActivateProfileCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region DeactivateProfileCommandHandler
    // =========================================================================

    [Fact]
    public async Task Deactivate_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var profile = MakeProfile(); // active
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(profile);

        var cmd = new DeactivateProfileCommand(Guid.NewGuid());
        var handler = new DeactivateProfileCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(profile.IsActive);
        _repo.Verify(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deactivate_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Profile?)null);

        var cmd = new DeactivateProfileCommand(Guid.NewGuid());
        var handler = new DeactivateProfileCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deactivate_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new DeactivateProfileCommand(Guid.NewGuid());
        var handler = new DeactivateProfileCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deactivate_WhenAlreadyInactive_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var profile = MakeProfile();
        profile.Deactivate(ActorId.Create("user-001")); // inactive now

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(profile);

        var cmd = new DeactivateProfileCommand(Guid.NewGuid());
        var handler = new DeactivateProfileCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region CreateProfileCommandHandler — auto-assignment (FS-06)
    // =========================================================================

    [Fact]
    public async Task Create_WhenNoMatchingRule_CreatesProfileWithoutTemplate()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        SetupNoMatchingRules();
        SetupValidProfileReferences(tenantId, userId, roleId);

        var cmd = new CreateProfileCommand(tenantId, userId, roleId, null);
        var result = await MakeCreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _templateRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenMatchingRuleExists_AutoAssignsTemplate()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        SetupValidProfileReferences(tenantId, userId, roleId);

        var rule = Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule.Create(
            TenantId.Load(tenantId), TemplateId.Load(Guid.NewGuid()),
            RoleId.Load(roleId), 10, ActorId.Create("admin")).Value;

        var ruleCallCount = 0;
        _ruleRepo.Setup(r => r.GetActiveByTenantAndRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => ruleCallCount++)
            .ReturnsAsync(new List<Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule> { rule });

        var template = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate.Create(
            TenantId.Load(tenantId), RoleId.Load(roleId),
            SystemSuiteId.Load(Guid.NewGuid()), ActorId.Create("admin")).Value;
        template.AddItem(ExclusiveArcTarget.SystemSuite, IdValueObject.Create(),
            ActionId.Load(Guid.NewGuid()), true, false, ActorId.Create("admin"));
        template.Publish(ActorId.Create("admin"));
        _templateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var cmd = new CreateProfileCommand(tenantId, userId, roleId, null);
        var result = await MakeCreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(ruleCallCount > 0, "GetActiveByTenantAndRoleAsync should have been called");
    }

    [Fact]
    public async Task Create_WhenMatchingRuleAndTemplate_ProfileReceivesPermissions()
    {
        // G-043: materialización de permisos. La plantilla publicada auto-asignada debe dejar
        // permisos en el agregado Profile ANTES de persistir (un único Save). Antes esto se hacía
        // en un segundo Save cuyo fallo se tragaba (permissionCount=0). Ahora se captura el perfil
        // en AddAsync y se verifica que trae permisos.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        SetupValidProfileReferences(tenantId, userId, roleId);

        var capturedProfile = (Profile?)null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>()))
            .Callback<Profile, CancellationToken>((p, _) => capturedProfile = p)
            .Returns(Task.CompletedTask);

        var template = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate.Create(
            TenantId.Load(tenantId), RoleId.Load(roleId),
            SystemSuiteId.Load(Guid.NewGuid()), ActorId.Create("admin")).Value;
        template.AddItem(ExclusiveArcTarget.SystemSuite, IdValueObject.Create(),
            ActionId.Load(Guid.NewGuid()), true, false, ActorId.Create("admin"));
        template.Publish(ActorId.Create("admin"));

        var rule = Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule.Create(
            TenantId.Load(tenantId), TemplateId.Load(template.Props.Id.GetValue()),
            RoleId.Load(roleId), 10, ActorId.Create("admin")).Value;

        _ruleRepo.Setup(r => r.GetActiveByTenantAndRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule> { rule });
        _templateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var cmd = new CreateProfileCommand(tenantId, userId, roleId, null);
        var result = await MakeCreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedProfile);
        Assert.NotEmpty(capturedProfile!.Permissions);
    }

    [Fact]
    public async Task Create_WhenAutoAssignedTemplateIsUnpublished_ReturnsFailureWithCode()
    {
        // G-043 (desenmascarar): si la plantilla referida por la regla NO está publicada,
        // AssignTemplate falla. Antes ese fallo se tragaba y el perfil quedaba sin permisos.
        // Ahora el comando falla con un código estable y NO persiste el perfil.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        SetupValidProfileReferences(tenantId, userId, roleId);

        var draftTemplate = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate.Create(
            TenantId.Load(tenantId), RoleId.Load(roleId),
            SystemSuiteId.Load(Guid.NewGuid()), ActorId.Create("admin")).Value;
        draftTemplate.AddItem(ExclusiveArcTarget.SystemSuite, IdValueObject.Create(),
            ActionId.Load(Guid.NewGuid()), true, false, ActorId.Create("admin"));
        // NOTA: NO se publica → AssignTemplate debe rechazarla.

        var rule = Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule.Create(
            TenantId.Load(tenantId), TemplateId.Load(draftTemplate.Props.Id.GetValue()),
            RoleId.Load(roleId), 10, ActorId.Create("admin")).Value;

        _ruleRepo.Setup(r => r.GetActiveByTenantAndRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule> { rule });
        _templateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(draftTemplate);

        var cmd = new CreateProfileCommand(tenantId, userId, roleId, null);
        var result = await MakeCreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("PROFILE_TEMPLATE_ASSIGN_FAILED", result.Error);
        _repo.Verify(r => r.AddAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenRuleReferencesMissingTemplate_ReturnsFailureWithCode()
    {
        // G-043 (desenmascarar): una regla activa que apunta a una plantilla inexistente es una
        // inconsistencia de configuración que ahora aflora en vez de tragarse (permissionCount=0).
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        SetupValidProfileReferences(tenantId, userId, roleId);

        var rule = Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule.Create(
            TenantId.Load(tenantId), TemplateId.Load(Guid.NewGuid()),
            RoleId.Load(roleId), 10, ActorId.Create("admin")).Value;

        _ruleRepo.Setup(r => r.GetActiveByTenantAndRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule> { rule });
        _templateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Evolith.Ums.Domain.Authorization.Template.PermissionTemplate?)null);

        var cmd = new CreateProfileCommand(tenantId, userId, roleId, null);
        var result = await MakeCreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("PROFILE_TEMPLATE_MISSING", result.Error);
        _repo.Verify(r => r.AddAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenMultipleRulesMatch_OnlyQueriesTemplateOnce()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        SetupValidProfileReferences(tenantId, userId, roleId);

        var template = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate.Create(
            TenantId.Load(tenantId), RoleId.Load(roleId),
            SystemSuiteId.Load(Guid.NewGuid()), ActorId.Create("admin")).Value;
        template.AddItem(ExclusiveArcTarget.SystemSuite, IdValueObject.Create(),
            ActionId.Load(Guid.NewGuid()), true, false, ActorId.Create("admin"));
        template.Publish(ActorId.Create("admin"));

        var highPriorityRule = Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule.Create(
            TenantId.Load(tenantId), TemplateId.Load(template.Props.Id.GetValue()), RoleId.Load(roleId), 100, ActorId.Create("admin")).Value;
        var lowPriorityRule = Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule.Create(
            TenantId.Load(tenantId), TemplateId.Load(Guid.NewGuid()), RoleId.Load(roleId), 10, ActorId.Create("admin")).Value;

        _ruleRepo.Setup(r => r.GetActiveByTenantAndRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule> { highPriorityRule, lowPriorityRule });
        _templateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var cmd = new CreateProfileCommand(tenantId, userId, roleId, null);
        var result = await MakeCreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // template repo is queried at most once (for the highest priority rule)
        _templateRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.AtMostOnce);
    }

    #endregion

    #region AssignTemplate — superseder por línea (ADR-UMS-106, [[G-272]])

    private static Evolith.Ums.Domain.Authorization.Template.PermissionTemplate MakePlantilla(
        Guid tenantId, Guid roleId, Guid suiteId)
    {
        var plantilla = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate.Create(
            TenantId.Load(tenantId), RoleId.Load(roleId), SystemSuiteId.Load(suiteId), ActorId.Create("admin")).Value;
        plantilla.AddItem(ExclusiveArcTarget.Module, IdValueObject.Create(), ActionId.Load(Guid.NewGuid()), true, false, ActorId.Create("admin"));
        plantilla.Publish(ActorId.Create("admin"));
        return plantilla;
    }

    private AssignProfileTemplateCommandHandler MakeAssignHandler()
        => new(_repo.Object, _templateRepo.Object, _ctx.Object, _scopePolicy.Object, _delegationAccess.Object);

    [Fact]
    public async Task AsignarVersionNueva_SupersedeALaAnteriorDeSuLinea()
    {
        // El defecto de [[G-272]]: la versión siguiente SUMABA sus permisos a los de la anterior.
        // Medido en UAT — perfiles de 94 a 188, de 407 a 528—, exactamente la suma de las dos.
        var perfil = MakeProfile();
        var tenantId = perfil.TenantId.GetValue();
        var roleId = perfil.RoleId.GetValue();
        var suiteId = Guid.NewGuid();

        var v1 = MakePlantilla(tenantId, roleId, suiteId);
        var v2 = MakePlantilla(tenantId, roleId, suiteId);
        Assert.True(perfil.AssignTemplate(v1, ActorId.Create("admin")).IsSuccess);

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(perfil);
        _templateRepo.Setup(r => r.GetByIdAsync(v2.GetId().GetValue(), It.IsAny<CancellationToken>())).ReturnsAsync(v2);
        _templateRepo.Setup(r => r.GetByTenantRoleSuiteAsync(tenantId, roleId, suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Evolith.Ums.Domain.Authorization.Template.PermissionTemplate> { v1, v2 });

        var result = await MakeAssignHandler().Handle(
            new AssignProfileTemplateCommand(perfil.GetId().GetValue(), v2.GetId().GetValue()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var vivos = perfil.Permissions.Where(p => p.IsActive).ToList();
        Assert.Single(vivos);
        Assert.Equal(v2.GetId().GetValue(), vivos[0].TemplateId.GetValue());
    }

    [Fact]
    public async Task AsignarPlantillaDeOtraLinea_ConservaLaQueYaTenia()
    {
        // La otra mitad, y es la que hizo revertir el primer intento de arreglo: el modelo admite
        // VARIAS plantillas por perfil a propósito, con resolución deny-wins entre ellas. Reemplazar
        // todo lo materializado dejaría esa regla inalcanzable.
        var perfil = MakeProfile();
        var tenantId = perfil.TenantId.GetValue();
        var roleId = perfil.RoleId.GetValue();

        var deLaSuiteA = MakePlantilla(tenantId, roleId, Guid.NewGuid());
        var suiteB = Guid.NewGuid();
        var deLaSuiteB = MakePlantilla(tenantId, roleId, suiteB);
        Assert.True(perfil.AssignTemplate(deLaSuiteA, ActorId.Create("admin")).IsSuccess);

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(perfil);
        _templateRepo.Setup(r => r.GetByIdAsync(deLaSuiteB.GetId().GetValue(), It.IsAny<CancellationToken>())).ReturnsAsync(deLaSuiteB);
        _templateRepo.Setup(r => r.GetByTenantRoleSuiteAsync(tenantId, roleId, suiteB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Evolith.Ums.Domain.Authorization.Template.PermissionTemplate> { deLaSuiteB });

        var result = await MakeAssignHandler().Handle(
            new AssignProfileTemplateCommand(perfil.GetId().GetValue(), deLaSuiteB.GetId().GetValue()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var vivos = perfil.Permissions.Where(p => p.IsActive).ToList();
        Assert.Equal(2, vivos.Count);
        Assert.Contains(vivos, p => p.TemplateId.GetValue() == deLaSuiteA.GetId().GetValue());
        Assert.Contains(vivos, p => p.TemplateId.GetValue() == deLaSuiteB.GetId().GetValue());
    }

    [Fact]
    public async Task DesligarPlantilla_RetiraSusPermisosDelPerfil()
    {
        var perfil = MakeProfile();
        var plantilla = MakePlantilla(perfil.TenantId.GetValue(), perfil.RoleId.GetValue(), Guid.NewGuid());
        Assert.True(perfil.AssignTemplate(plantilla, ActorId.Create("admin")).IsSuccess);

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(perfil);

        var handler = new UnassignProfileTemplateCommandHandler(
            _repo.Object, _ctx.Object, _scopePolicy.Object, _delegationAccess.Object);

        var result = await handler.Handle(
            new UnassignProfileTemplateCommand(perfil.GetId().GetValue(), plantilla.GetId().GetValue()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(perfil.Permissions.Where(p => p.IsActive));
    }

    #endregion
}
