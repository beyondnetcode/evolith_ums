namespace Evolith.Ums.Application.Test.Tenants.AddBranch;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Tenant.Branch.Commands;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Kernel;

public class AddBranchCommandHandlerTests
{
    private readonly Mock<ITenantRepository> _tenantRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserContext> _userContextMock;
    private readonly Mock<ITenantScopePolicy> _scopePolicyMock;
    private readonly AddBranchCommandHandler _handler;

    public AddBranchCommandHandlerTests()
    {
        _tenantRepositoryMock = new Mock<ITenantRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _tenantRepositoryMock.Setup(r => r.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _userContextMock = new Mock<IUserContext>();
        _scopePolicyMock = new Mock<ITenantScopePolicy>();
        _scopePolicyMock.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _handler = new AddBranchCommandHandler(_tenantRepositoryMock.Object, _userContextMock.Object, _scopePolicyMock.Object);
    }

    private static AddBranchCommand ValidCommand => new(
        TenantId: Guid.NewGuid(),
        Code: "BR-001",
        Name: "Branch One",
        GeofencingMetadata: null);

    #region Handle - Success Scenarios

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        var command = ValidCommand with { TenantId = tenantId };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(tenantId, result.Value.TenantId);
    }

    // G-161: cuando la verificación autoritativa de unicidad (que ignora el filtro global por inquilino)
    // detecta el código ya existente —incluso si la colección en memoria del agregado está vacía por el
    // filtro cross-tenant— la creación se rechaza con 409 (BranchCodeNotUnique), no un 201 engañoso.
    [Fact]
    public async Task Handle_WhenBranchCodeExistsCrossTenant_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateTenant(); // agregado sin sucursales (colección vaciada por el filtro global)
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _tenantRepositoryMock.Setup(r => r.BranchCodeExistsAsync(tenantId, "BR-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var command = ValidCommand with { TenantId = tenantId };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.BranchCodeNotUnique, result.Error);
    }

    [Fact]
    public async Task Handle_WithGeofencingMetadata_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        var command = ValidCommand with { TenantId = tenantId, GeofencingMetadata = "{\"lat\": 40.7128, \"lon\": -74.0060}" };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesTenant()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        var command = ValidCommand with { TenantId = tenantId };

        await _handler.Handle(command, CancellationToken.None);

        _tenantRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CallsSaveEntities()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _tenantRepositoryMock.Setup(r => r.UnitOfWork).Returns(unitOfWorkMock.Object);
        var command = ValidCommand with { TenantId = tenantId };

        await _handler.Handle(command, CancellationToken.None);

        unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Handle - Failure Scenarios

    [Fact]
    public async Task Handle_WhenUserIdIsEmpty_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("");
        var command = ValidCommand with { TenantId = tenantId };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserIdIsNull_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns((string?)null);
        var command = ValidCommand with { TenantId = tenantId };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task Handle_WhenTenantNotFound_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        var command = ValidCommand with { TenantId = tenantId };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant was not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenBranchCodeAlreadyExists_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateTenant();
        tenant.AddBranch(Code.Create("BR-001"), Name.Create("Existing Branch"), ActorId.Create("user-001"), null);
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        var command = ValidCommand with { TenantId = tenantId, Code = "BR-001" };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_WhenEmptyBranchCode_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        var command = ValidCommand with { TenantId = tenantId, Code = "" };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_WhenTenantIsNotManagementOwner_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        _scopePolicyMock.Setup(s => s.EnsureManagementOwnerScopeAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("AUTH_015: Tenant is not marked as management owner."));
        var tenant = CreateTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        var command = ValidCommand with { TenantId = tenantId };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("management owner", result.Error, StringComparison.OrdinalIgnoreCase);
        _tenantRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    private static Tenant CreateTenant()
    {
        return Tenant.Create(
            Code.Create("TEST-001"),
            Name.Create("Test Tenant"),
            OrganizationType.INTERNAL,
            ActorId.Create("user-001"),
            IdpStrategy.InternalBcrypt,
            null,
            null).Value;
    }
}
