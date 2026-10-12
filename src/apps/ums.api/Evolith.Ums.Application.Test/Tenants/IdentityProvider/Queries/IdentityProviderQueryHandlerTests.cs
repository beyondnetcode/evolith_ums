namespace Evolith.Ums.Application.Test.Tenants.IdentityProvider.Queries;

using Evolith.Ums.Application.Identity.Tenant.IdentityProvider.Queries;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Kernel;

public class IdentityProviderQueryHandlerTests
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
    #region GetIdentityProvidersByTenantIdQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetIdentityProviders_WhenTenantFound_ReturnsProviders()
    {
        var tenant = MakeTenant();
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), Description.Create("Azure AD Provider"), IdpStrategy.AzureAd, ActorId.Create("user-001"));
        tenant.RegisterIdentityProvider(Code.Create("IDP-002"), Name.Create("Okta"), Description.Create("Okta Provider"), IdpStrategy.Okta, ActorId.Create("user-001"));

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var query = new GetIdentityProvidersByTenantIdQuery(tenant.Props.Id.GetValue());
        var handler = new GetIdentityProvidersByTenantIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("IDP-001", result.Value[0].Code);
        Assert.Equal("AzureAd", result.Value[0].Strategy);
    }

    [Fact]
    public async Task GetIdentityProviders_WhenTenantHasNoProviders_ReturnsEmptyList()
    {
        var tenant = MakeTenant();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var query = new GetIdentityProvidersByTenantIdQuery(tenant.Props.Id.GetValue());
        var handler = new GetIdentityProvidersByTenantIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetIdentityProviders_WhenTenantNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Tenant?)null);

        var query = new GetIdentityProvidersByTenantIdQuery(Guid.NewGuid());
        var handler = new GetIdentityProvidersByTenantIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant not found", result.Error);
    }

    [Fact]
    public async Task GetIdentityProviders_ReturnsActiveStatus()
    {
        var tenant = MakeTenant();
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), Description.Create("Azure AD Provider"), IdpStrategy.AzureAd, ActorId.Create("user-001"));
        var idp = tenant.IdentityProviders.First();
        tenant.ActivateIdentityProvider(idp.GetId(), ActorId.Create("user-001"));

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var query = new GetIdentityProvidersByTenantIdQuery(tenant.Props.Id.GetValue());
        var handler = new GetIdentityProvidersByTenantIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value[0].IsActive);
    }

    #endregion
}
