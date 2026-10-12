namespace Evolith.Ums.Application.Test.Tenants.Branch;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Tenant.Branch.Queries;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Kernel;

public class BranchQueryHandlerTests
{
    private readonly Mock<ITenantRepository> _repo = new();

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
    #region GetBranchesByTenantIdQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetBranches_WhenTenantFound_ReturnsBranches()
    {
        var tenant = MakeTenant();
        tenant.AddBranch(Code.Create("BR-001"), Name.Create("Branch One"), ActorId.Create("user-001"), null);
        tenant.AddBranch(Code.Create("BR-002"), Name.Create("Branch Two"), ActorId.Create("user-001"), null);

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var query = new GetBranchesByTenantIdQuery(tenant.Props.Id.GetValue());
        var handler = new GetBranchesByTenantIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("BR-001", result.Value[0].Code);
        Assert.Equal("Branch One", result.Value[0].Name);
    }

    [Fact]
    public async Task GetBranches_WhenTenantHasNoBranches_ReturnsEmptyList()
    {
        var tenant = MakeTenant();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var query = new GetBranchesByTenantIdQuery(tenant.Props.Id.GetValue());
        var handler = new GetBranchesByTenantIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetBranches_WhenTenantNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Tenant?)null);

        var query = new GetBranchesByTenantIdQuery(Guid.NewGuid());
        var handler = new GetBranchesByTenantIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant not found", result.Error);
    }

    [Fact]
    public async Task GetBranches_WithGeofencingMetadata_ReturnsMetadata()
    {
        var tenant = MakeTenant();
        tenant.AddBranch(Code.Create("BR-001"), Name.Create("Branch One"), ActorId.Create("user-001"), Value.Create("{\"lat\": 40.7128}"));

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var query = new GetBranchesByTenantIdQuery(tenant.Props.Id.GetValue());
        var handler = new GetBranchesByTenantIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("{\"lat\": 40.7128}", result.Value[0].GeofencingMetadata);
    }

    #endregion
}
