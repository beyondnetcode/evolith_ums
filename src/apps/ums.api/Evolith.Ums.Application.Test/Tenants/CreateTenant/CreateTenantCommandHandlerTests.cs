namespace Evolith.Ums.Application.Test.Tenants.CreateTenant;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Tenant.Commands;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Kernel;

public class CreateTenantCommandHandlerTests
{
    private readonly Mock<ITenantRepository> _tenantRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserContext> _userContextMock;
    private readonly CreateTenantCommandHandler _handler;

    public CreateTenantCommandHandlerTests()
    {
        _tenantRepositoryMock = new Mock<ITenantRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _tenantRepositoryMock.Setup(r => r.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _userContextMock = new Mock<IUserContext>();
        _handler = new CreateTenantCommandHandler(_tenantRepositoryMock.Object, _userContextMock.Object);
    }

    private static CreateTenantCommand ValidCommand => new(
        Code: "TEST-001",
        Name: "Test Tenant",
        Type: "INTERNAL",
        IdpStrategy: "InternalBcrypt",
        CompanyReference: null);

    #region Handle - Success Scenarios

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccess()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        _tenantRepositoryMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var result = await _handler.Handle(ValidCommand, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEqual(Guid.Empty, result.Value.TenantId);
    }

    [Fact]
    public async Task Handle_WithValidCommand_SavesToRepository()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        _tenantRepositoryMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        await _handler.Handle(ValidCommand, CancellationToken.None);

        _tenantRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CallsSaveEntities()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        _tenantRepositoryMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _tenantRepositoryMock.Setup(r => r.UnitOfWork).Returns(unitOfWorkMock.Object);

        await _handler.Handle(ValidCommand, CancellationToken.None);

        unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExternalIdpStrategy_ReturnsSuccess()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        _tenantRepositoryMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        var command = ValidCommand with { IdpStrategy = "AzureAd" };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_WithCompanyReference_ReturnsSuccess()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        _tenantRepositoryMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        var command = ValidCommand with { CompanyReference = "COMP-123" };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_WithNullIdpStrategy_UsesDefaultInternalBcrypt()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        _tenantRepositoryMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        var command = ValidCommand with { IdpStrategy = null };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    #endregion

    #region Handle - Failure Scenarios

    [Fact]
    public async Task Handle_WhenUserIdIsEmpty_ReturnsFailure()
    {
        _userContextMock.Setup(u => u.UserId).Returns("");

        var result = await _handler.Handle(ValidCommand, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserIdIsNull_ReturnsFailure()
    {
        _userContextMock.Setup(u => u.UserId).Returns((string?)null);

        var result = await _handler.Handle(ValidCommand, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserIdIsWhitespace_ReturnsFailure()
    {
        _userContextMock.Setup(u => u.UserId).Returns("   ");

        var result = await _handler.Handle(ValidCommand, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task Handle_WhenTenantCodeAlreadyExists_ReturnsFailure()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var existingTenant = Tenant.Create(
            Code.Create("TEST-001"),
            Name.Create("Existing Tenant"),
            OrganizationType.INTERNAL,
            ActorId.Create("user-001"),
            IdpStrategy.InternalBcrypt,
            null,
            null).Value;
        _tenantRepositoryMock.Setup(r => r.GetByCodeAsync("TEST-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTenant);

        var result = await _handler.Handle(ValidCommand, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant code already exists", result.Error);
    }

    [Fact]
    public async Task Handle_WhenDomainCreationFails_ReturnsFailure()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        _tenantRepositoryMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        var command = new CreateTenantCommand(
            Code: "",
            Name: "Test Tenant",
            Type: "INTERNAL",
            IdpStrategy: null,
            CompanyReference: null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion
}
