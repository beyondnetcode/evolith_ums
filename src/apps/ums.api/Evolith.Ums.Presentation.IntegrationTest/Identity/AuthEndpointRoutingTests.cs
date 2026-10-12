using Microsoft.AspNetCore.Routing;
using FluentAssertions;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity;

public sealed class AuthEndpointRoutingTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly EndpointDataSource _endpointDataSource;

    public AuthEndpointRoutingTests(UmsApiWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        _endpointDataSource = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();
    }

    [Fact]
    public void VisualLoginAndClientAuthentication_ShouldBeMappedToDifferentApiRoots()
    {
        var routes = _endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        routes.Should().Contain("/api/v1/auth/login");
        routes.Should().Contain("/api/v1/client/authenticate");
        routes.Should().NotContain("/api/v1/auth/client/authenticate");
    }

    /// <summary>
    /// El carril de satélite tiene su propio cambio de perfil (ADR-0156 §8). Los dos endpoints
    /// coexisten a propósito: `/auth/switch-profile` sirve al portal —forma de respuesta propia y
    /// cookie de sesión— y `/client/switch-profile` al portador semántico, que no tiene ni puede
    /// usar esa cookie. Que este test los exija a los dos es lo que impide que alguien «unifique»
    /// el carril de satélite dentro del endpoint del portal, que valida el token a mano con
    /// `ValidateIssuer=false` (G-201).
    /// </summary>
    [Fact]
    public void SwitchProfile_ShouldExistOnBothRails()
    {
        var routes = _endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        routes.Should().Contain("/api/v1/auth/switch-profile");
        routes.Should().Contain("/api/v1/client/switch-profile");
    }
}
