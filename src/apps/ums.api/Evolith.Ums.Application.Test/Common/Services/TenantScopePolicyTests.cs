namespace Evolith.Ums.Application.Test.Common.Services;

using Moq;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Kernel;
using Xunit;

public sealed class TenantScopePolicyTests
{
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ITenantRepository> _tenantRepository = new();

    private TenantScopePolicy CreateSut()
        => new(_tenantContext.Object, _userContext.Object, _tenantRepository.Object);

    private static Tenant BuildTenant(bool isManagementOwner)
        => Tenant.Create(
            Code.Create("TEN-001"),
            Name.Create("Tenant One"),
            OrganizationType.INTERNAL,
            ActorId.Create("system"),
            IdpStrategy.InternalBcrypt,
            null,
            null,
            tenantId: TenantId.Load(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            isManagementOwner: isManagementOwner).Value;

    [Fact]
    public void ResolveQueryScope_ReturnsNull_WhenInternalAdmin()
    {
        // Internal admin has cross-tenant visibility: no scope filter, even when its own
        // OrganizationId is populated (e.g. the INTERNAL_ADMIN dev tenant).
        _tenantContext.SetupGet(x => x.IsInternalAdmin).Returns(true);
        _tenantContext.SetupGet(x => x.OrganizationId).Returns(Guid.NewGuid());

        var result = CreateSut().ResolveQueryScope();

        Assert.Null(result);
    }

    [Fact]
    public void ResolveQueryScope_ReturnsOrganizationId_WhenContextHasOne()
    {
        var tenantId = Guid.NewGuid();
        _tenantContext.SetupGet(x => x.IsInternalAdmin).Returns(false);
        _tenantContext.SetupGet(x => x.OrganizationId).Returns(tenantId);
        _userContext.SetupGet(x => x.TenantId).Returns((string?)null);

        var result = CreateSut().ResolveQueryScope();

        Assert.Equal(tenantId, result);
    }

    [Fact]
    public void ResolveQueryScope_ReturnsUserTenant_WhenContextMissing()
    {
        var tenantId = Guid.NewGuid();
        _tenantContext.SetupGet(x => x.IsInternalAdmin).Returns(false);
        _tenantContext.SetupGet(x => x.OrganizationId).Returns((Guid?)null);
        _userContext.SetupGet(x => x.TenantId).Returns(tenantId.ToString());

        var result = CreateSut().ResolveQueryScope();

        Assert.Equal(tenantId, result);
    }

    [Fact]
    public void ResolveQueryScope_ReturnsNull_WhenNoTenantInformationExists()
    {
        _tenantContext.SetupGet(x => x.IsInternalAdmin).Returns(false);
        _tenantContext.SetupGet(x => x.OrganizationId).Returns((Guid?)null);
        _userContext.SetupGet(x => x.TenantId).Returns((string?)null);

        var result = CreateSut().ResolveQueryScope();

        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureManagementOwnerScopeAsync_AllowsOwnManagementTenant()
    {
        var tenantId = Guid.NewGuid();
        var tenant = BuildTenant(isManagementOwner: true);

        _tenantContext.SetupGet(x => x.OrganizationId).Returns(tenantId);
        _userContext.SetupGet(x => x.TenantId).Returns(tenantId.ToString());
        _tenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var result = await CreateSut().EnsureManagementOwnerScopeAsync(tenantId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureManagementOwnerScopeAsync_FailsWhenTenantIsNotManagementOwner()
    {
        var tenantId = Guid.NewGuid();
        var tenant = BuildTenant(isManagementOwner: false);

        _tenantContext.SetupGet(x => x.OrganizationId).Returns(tenantId);
        _userContext.SetupGet(x => x.TenantId).Returns(tenantId.ToString());
        _tenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var result = await CreateSut().EnsureManagementOwnerScopeAsync(tenantId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("management owner", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureManagementOwnerScopeAsync_FailsWhenTenantMismatch()
    {
        var currentTenantId = Guid.NewGuid();
        var targetTenantId = Guid.NewGuid();
        var tenant = BuildTenant(isManagementOwner: true);

        _tenantContext.SetupGet(x => x.OrganizationId).Returns(currentTenantId);
        _userContext.SetupGet(x => x.TenantId).Returns(currentTenantId.ToString());
        _tenantRepository.Setup(r => r.GetByIdAsync(targetTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var result = await CreateSut().EnsureManagementOwnerScopeAsync(targetTenantId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant mismatch", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
