namespace Evolith.Ums.Application.Test.Identity.Tenant.Commands;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Tenant.Commands;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant;

public class SetManagementOwnerCommandHandlerTests
{
    private readonly Mock<ITenantRepository> _tenantRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserContext> _userContextMock;
    private readonly SetManagementOwnerCommandHandler _handler;

    public SetManagementOwnerCommandHandlerTests()
    {
        _tenantRepositoryMock = new Mock<ITenantRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _tenantRepositoryMock.Setup(r => r.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        // G-045: el handler ahora verifica la unicidad del management owner leyendo todos los
        // tenants antes de persistir. Por defecto no existe ningún otro owner.
        _tenantRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tenant>());

        _userContextMock = new Mock<IUserContext>();
        _handler = new SetManagementOwnerCommandHandler(_tenantRepositoryMock.Object, _userContextMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateTenant(isManagementOwner: false);
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var result = await _handler.Handle(new SetManagementOwnerCommand(tenantId, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(tenant.IsManagementOwner);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesTenant()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateTenant(isManagementOwner: false);
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        await _handler.Handle(new SetManagementOwnerCommand(tenantId, true), CancellationToken.None);

        _tenantRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserIdIsEmpty_ReturnsFailure()
    {
        _userContextMock.Setup(u => u.UserId).Returns("");

        var result = await _handler.Handle(new SetManagementOwnerCommand(Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserIdIsNull_ReturnsFailure()
    {
        _userContextMock.Setup(u => u.UserId).Returns((string?)null);

        var result = await _handler.Handle(new SetManagementOwnerCommand(Guid.NewGuid(), true), CancellationToken.None);

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

        var result = await _handler.Handle(new SetManagementOwnerCommand(tenantId, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant was not found", result.Error);
        _tenantRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAnotherManagementOwnerExists_ReturnsFailure()
    {
        // G-045: otorgar la propiedad de gestión a un segundo tenant viola el índice parcial
        // único IX_Tenants_SingleManagementOwner. El handler lo detecta antes de persistir y
        // devuelve un fallo (que presentación mapea a 409) en vez de un 500 por violación de índice.
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateTenant(isManagementOwner: false);
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        var existingOwner = CreateTenant(isManagementOwner: true);
        _tenantRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tenant> { existingOwner });

        var result = await _handler.Handle(new SetManagementOwnerCommand(tenantId, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        // G-037/G-045: código estable de dominio (presentación lo mapea a 409).
        Assert.Equal(DomainErrors.Tenant.ManagementOwnerAlreadyExists, result.Error);
        _tenantRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Tenant CreateTenant(bool isManagementOwner)
    {
        return Tenant.Create(
            Code.Create("TEN-001"),
            Name.Create("Tenant 001"),
            OrganizationType.INTERNAL,
            ActorId.Create("user-001"),
            IdpStrategy.InternalBcrypt,
            null,
            null,
            null,
            isManagementOwner).Value;
    }
}
