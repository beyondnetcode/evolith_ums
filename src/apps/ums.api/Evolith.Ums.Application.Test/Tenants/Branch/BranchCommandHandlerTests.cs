namespace Evolith.Ums.Application.Test.Tenants.Branch;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Tenant.Branch.Commands;
using Evolith.Ums.Application.Identity.Tenant.Branch.Queries;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Kernel;

public class BranchCommandHandlerTests
{
    private readonly Mock<ITenantRepository> _repo = new();
    private readonly Mock<IUserAccountRepository> _userRepo = new();
    private readonly Mock<IProfileRepository> _profileRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserContext> _ctx = new();
    private readonly Mock<ITenantScopePolicy> _scopePolicy = new();

    public BranchCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _scopePolicy.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _userRepo.Setup(r => r.CountActiveByBranchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _profileRepo.Setup(r => r.CountActiveByBranchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private static Tenant MakeTenant()
    {
        return Tenant.Create(
            Code.Create("TEN-001"),
            Name.Create("Test Tenant"),
            OrganizationType.INTERNAL,
            ActorId.Create("user-001"),
            IdpStrategy.InternalBcrypt,
            null,
            null).Value;
    }

    // =========================================================================
    #region DeactivateBranchCommandHandler
    // =========================================================================

    [Fact]
    public async Task DeactivateBranch_WithValidCommand_ReturnsSuccess()
    {
        var tenant = MakeTenant();
        tenant.AddBranch(Code.Create("BR-001"), Name.Create("Branch One"), ActorId.Create("user-001"), null);
        var branchId = tenant.Branches.First().GetId().GetValue();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var cmd = new DeactivateBranchCommand(tenant.Props.Id.GetValue(), branchId);
        var handler = new DeactivateBranchCommandHandler(_repo.Object, _userRepo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.False(tenant.Branches.First().IsActive);
        _repo.Verify(r => r.UpdateAsync(tenant, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateBranch_WhenTenantNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Tenant?)null);

        var cmd = new DeactivateBranchCommand(Guid.NewGuid(), Guid.NewGuid());
        var handler = new DeactivateBranchCommandHandler(_repo.Object, _userRepo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant was not found", result.Error);
    }

    [Fact]
    public async Task DeactivateBranch_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new DeactivateBranchCommand(Guid.NewGuid(), Guid.NewGuid());
        var handler = new DeactivateBranchCommandHandler(_repo.Object, _userRepo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task DeactivateBranch_WhenBranchNotFound_ReturnsFailure()
    {
        var tenant = MakeTenant();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var cmd = new DeactivateBranchCommand(tenant.Props.Id.GetValue(), Guid.NewGuid());
        var handler = new DeactivateBranchCommandHandler(_repo.Object, _userRepo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region ReactivateBranchCommandHandler
    // =========================================================================

    [Fact]
    public async Task ReactivateBranch_WithValidCommand_ReturnsSuccess()
    {
        var tenant = MakeTenant();
        tenant.AddBranch(Code.Create("BR-001"), Name.Create("Branch One"), ActorId.Create("user-001"), null);
        var branch = tenant.Branches.First();
        tenant.DeactivateBranch(branch.GetId(), ActorId.Create("user-001"));

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var cmd = new ReactivateBranchCommand(tenant.Props.Id.GetValue(), branch.GetId().GetValue());
        var handler = new ReactivateBranchCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(branch.IsActive);
        _repo.Verify(r => r.UpdateAsync(tenant, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReactivateBranch_WhenTenantNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Tenant?)null);

        var cmd = new ReactivateBranchCommand(Guid.NewGuid(), Guid.NewGuid());
        var handler = new ReactivateBranchCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant was not found", result.Error);
    }

    [Fact]
    public async Task ReactivateBranch_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new ReactivateBranchCommand(Guid.NewGuid(), Guid.NewGuid());
        var handler = new ReactivateBranchCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task ReactivateBranch_WhenBranchNotFound_ReturnsFailure()
    {
        var tenant = MakeTenant();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var cmd = new ReactivateBranchCommand(tenant.Props.Id.GetValue(), Guid.NewGuid());
        var handler = new ReactivateBranchCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region CloseBranchCommandHandler (ADR-0164)
    // =========================================================================

    [Fact]
    public async Task CloseBranch_WithValidCommand_ReturnsSuccess()
    {
        var tenant = MakeTenant();
        tenant.AddBranch(Code.Create("BR-001"), Name.Create("Branch One"), ActorId.Create("user-001"), null);
        var branch = tenant.Branches.First();
        tenant.DeactivateBranch(branch.GetId(), ActorId.Create("user-001"));

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var cmd = new CloseBranchCommand(tenant.Props.Id.GetValue(), branch.GetId().GetValue());
        var handler = NuevoManejador();
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        // ADR-0164 §2.1: la sucursal NO desaparece de la colección. Antes esta misma prueba
        // afirmaba `Assert.Empty(tenant.Branches)`.
        Assert.Single(tenant.Branches);
        Assert.True(tenant.Branches.First().IsClosed);
        _repo.Verify(r => r.UpdateAsync(tenant, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseBranch_ConUsuariosActivos_Devuelve409ConElDesglose()
    {
        var tenant = MakeTenant();
        tenant.AddBranch(Code.Create("BR-001"), Name.Create("Branch One"), ActorId.Create("user-001"), null);
        var branch = tenant.Branches.First();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _userRepo.Setup(r => r.CountActiveByBranchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _profileRepo.Setup(r => r.CountActiveByBranchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var cmd = new CloseBranchCommand(tenant.Props.Id.GetValue(), branch.GetId().GetValue());
        var result = await NuevoManejador().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.True(BlockedOperationError.TryDecode(result.Error, out var codigo, out var deps));
        Assert.Equal(DomainErrors.Tenant.BranchHasLiveReferences, codigo);
        // El desglose nombra LAS DOS clases que bloquean, no solo la primera encontrada.
        Assert.Equal(2, deps.Count);
        Assert.Equal(2, deps.Single(d => d.EntityType == "UserAccount").Count);
        Assert.Equal(3, deps.Single(d => d.EntityType == "Profile").Count);
        Assert.False(tenant.Branches.First().IsClosed);
    }

    [Fact]
    public async Task CloseBranch_ConPerfilesActivosPeroSinUsuarios_TambienBloquea()
    {
        // Sin este caso la guarda de perfiles podría quedar muerta detrás de la de usuarios.
        var tenant = MakeTenant();
        tenant.AddBranch(Code.Create("BR-001"), Name.Create("Branch One"), ActorId.Create("user-001"), null);
        var branch = tenant.Branches.First();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _profileRepo.Setup(r => r.CountActiveByBranchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new CloseBranchCommand(tenant.Props.Id.GetValue(), branch.GetId().GetValue());
        var result = await NuevoManejador().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.True(BlockedOperationError.TryDecode(result.Error, out _, out var deps));
        Assert.Equal("Profile", Assert.Single(deps).EntityType);
    }

    [Fact]
    public async Task CloseBranch_WhenTenantNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Tenant?)null);

        var cmd = new CloseBranchCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = await NuevoManejador().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant was not found", result.Error);
    }

    [Fact]
    public async Task CloseBranch_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new CloseBranchCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = await NuevoManejador().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task CloseBranch_WhenBranchNotFound_ReturnsFailure()
    {
        var tenant = MakeTenant();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var cmd = new CloseBranchCommand(tenant.Props.Id.GetValue(), Guid.NewGuid());
        var result = await NuevoManejador().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region GetBranchesByTenantIdQueryHandler (ADR-0164: listas sin las cerradas)
    // =========================================================================

    [Fact]
    public async Task ListarSucursales_OcultaLasCerradasSalvoQueSePidanExpresamente()
    {
        var tenant = MakeTenant();
        tenant.AddBranch(Code.Create("BR-VIVA"), Name.Create("Sucursal viva"), ActorId.Create("user-001"), null);
        tenant.AddBranch(Code.Create("BR-CERRADA"), Name.Create("Sucursal cerrada"), ActorId.Create("user-001"), null);
        var cerrada = tenant.Branches.First(b => b.Code.GetValue() == "BR-CERRADA");
        tenant.CloseBranch(cerrada.GetId(), ActorId.Create("user-001"));

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        var handler = new GetBranchesByTenantIdQueryHandler(_repo.Object);

        var pordefecto = await handler.Handle(new GetBranchesByTenantIdQuery(tenant.Props.Id.GetValue()), CancellationToken.None);
        Assert.True(pordefecto.IsSuccess);
        Assert.Equal("BR-VIVA", Assert.Single(pordefecto.Value).Code);

        // Pedirlas explícitamente sí las trae, y marcadas: «oculto» no es «borrado».
        var conCerradas = await handler.Handle(
            new GetBranchesByTenantIdQuery(tenant.Props.Id.GetValue(), IncludeClosed: true), CancellationToken.None);
        Assert.Equal(2, conCerradas.Value.Count);
        var dtoCerrada = conCerradas.Value.Single(b => b.Code == "BR-CERRADA");
        Assert.True(dtoCerrada.IsClosed);
        Assert.NotNull(dtoCerrada.ClosedAtUtc);
    }

    #endregion

    private CloseBranchCommandHandler NuevoManejador()
        => new(_repo.Object, _userRepo.Object, _profileRepo.Object, _ctx.Object, _scopePolicy.Object);
}
