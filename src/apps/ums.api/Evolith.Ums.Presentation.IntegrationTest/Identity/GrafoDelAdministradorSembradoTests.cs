using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity;

/// <summary>
/// La siembra debe dejar al administrador con un grafo RESOLUBLE ([[G-286]]).
///
/// <para>No basta con que exista la cuenta: si su perfil apunta a un rol que el repositorio no
/// devuelve, el grafo no se puede construir y la identidad llega sin un solo scope. Mientras el
/// atajo de Admin Root estuvo encendido eso daba igual —concedia sin mirar—; en cuanto el aspecto
/// gobierna, ese administrador entra y no puede hacer nada.</para>
///
/// <para>Se comprueba en el anfitrion InMemory a proposito: es el que usa quien levanta UMS para
/// desarrollar, y es el unico que separa a esta suite de correr con el atajo APAGADO ([[G-280]]).</para>
/// </summary>
public sealed class GrafoDelAdministradorSembradoTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly UmsApiWebApplicationFactory _factory;

    public GrafoDelAdministradorSembradoTests(UmsApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task LaCuentaSembrada_TienePerfilYRolResoluble()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var contextoDeInquilino = sp.GetRequiredService<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        contextoDeInquilino.Initialize(Guid.Parse(CoreDevDataSeeder.EvolithTenantId), true);

        var cuentas = sp.GetRequiredService<IUserAccountRepository>();
        var administrador = await cuentas.GetByIdAsync(Guid.Parse(TestAuthHandler.AdministradorSembrado), ct);
        administrador.Should().NotBeNull("la siembra debe crear el administrador de EVOLITH");

        var perfiles = sp.GetRequiredService<IProfileRepository>();
        var suyos = await perfiles.GetByUserIdAsync(administrador!.GetId().GetValue(), ct);
        suyos.Should().NotBeEmpty("un administrador sin perfil no puede tener grafo");

        var roles = sp.GetRequiredService<IRoleRepository>();
        foreach (var perfil in suyos.Where(p => p.Props.IsActive))
        {
            var rol = await roles.GetByIdAsync(perfil.Props.RoleId.GetValue(), ct);
            rol.Should().NotBeNull(
                $"el perfil {perfil.GetId().GetValue()} apunta al rol {perfil.Props.RoleId.GetValue()}, y sin ese rol el grafo no se puede construir");
        }
    }

    [Fact]
    public async Task ElGrafoDelAdministradorSembrado_SeConstruyeYConcedeScopes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var contextoDeInquilino = sp.GetRequiredService<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        contextoDeInquilino.Initialize(Guid.Parse(CoreDevDataSeeder.EvolithTenantId), true);

        var cuentas = sp.GetRequiredService<IUserAccountRepository>();
        var administrador = await cuentas.GetByIdAsync(Guid.Parse(TestAuthHandler.AdministradorSembrado), ct);
        administrador.Should().NotBeNull();

        var constructor = sp.GetRequiredService<IAuthorizationGraphBuilder>();
        var grafo = await constructor.BuildAsync(
            administrador!, Guid.Parse(CoreDevDataSeeder.EvolithTenantId), Evolith.Ums.Domain.Identity.Auth.AuthMethod.Local(), null, ct);

        grafo.IsSuccess.Should().BeTrue($"el grafo del administrador sembrado debe resolverse; falló con «{grafo.Error}»");
        grafo.Value.Scopes.Should().NotBeEmpty("un administrador sin un solo scope no puede administrar nada");
    }

    [Fact]
    public async Task ElAdministradorActuandoEnOtroInquilino_LlegaConSusScopes()
    {
        // El camino EXACTO del andamiaje: la cabecera declara un inquilino ajeno —el de
        // administración interna—, la cuenta vive en EVOLITH y el grafo se construye en su casa.
        // Es donde se veía «Role not found for profile» y donde 36 pruebas morían con 403 ([[G-286]]).
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var contextoDeInquilino = sp.GetRequiredService<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        contextoDeInquilino.Initialize(Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId), true);

        var claims = new List<System.Security.Claims.Claim>();
        await Evolith.Ums.Presentation.Middleware.ScopesDelGrafo.AnadirAsync(
            claims,
            TestAuthHandler.AdministradorSembrado,
            CoreDevDataSeeder.InternalAdminTenantId,
            esAdminInterno: true,
            contextoDeInquilino,
            sp.GetRequiredService<IUserAccountRepository>(),
            sp.GetRequiredService<IAuthorizationGraphBuilder>(),
            ct);

        claims.Where(c => c.Type == "scope").Should().NotBeEmpty(
            "un administrador interno que actúa sobre otro inquilino conserva los permisos de su casa (ADR-UMS-105 D3)");
    }

    /// <summary>
    /// El catálogo sembrado debe cubrir TODO lo que el producto guarda ([[G-286]]).
    ///
    /// <para>El aspecto protege 30 permisos distintos; la siembra concedía 18. Los doce que faltaban
    /// —roles, promoción de roles con sus acciones propias APPROVE/CERTIFY/EXECUTE, aprobaciones,
    /// reglas de aprobación y auditoría— no los tenía NADIE: ni el administrador sembrado. Un
    /// permiso que ninguna plantilla concede es una pantalla que nadie puede abrir.</para>
    /// </summary>
    [Fact]
    public async Task ElAdministradorSembrado_AlcanzaTodoLoQueElProductoGuarda()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var contextoDeInquilino = sp.GetRequiredService<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        contextoDeInquilino.Initialize(Guid.Parse(CoreDevDataSeeder.EvolithTenantId), true);

        var cuentas = sp.GetRequiredService<IUserAccountRepository>();
        var administrador = await cuentas.GetByIdAsync(Guid.Parse(TestAuthHandler.AdministradorSembrado), ct);
        var constructor = sp.GetRequiredService<IAuthorizationGraphBuilder>();
        var grafo = await constructor.BuildAsync(
            administrador!, Guid.Parse(CoreDevDataSeeder.EvolithTenantId),
            Evolith.Ums.Domain.Identity.Auth.AuthMethod.Local(), "UMS", ct);

        grafo.IsSuccess.Should().BeTrue(grafo.Error);

        // Los mismos que exigen los `[AuthorizationAspect]` del producto.
        string[] guardados =
        [
            "approve_role_promotions.approve", "certify_role_promotions.certify",
            "execute_role_promotions.execute", "manage_approval_rules.manage",
            "manage_approvals.manage", "manage_config.manage", "manage_delegations.manage",
            "manage_flags.manage", "manage_params.manage", "manage_profiles.manage",
            "manage_role_promotions.manage", "manage_roles.manage", "manage_suites.manage",
            "manage_templates.manage", "manage_tenants.manage", "manage_users.manage",
            "view_approval_rules.view", "view_approvals.view", "view_audit.view",
            "view_config.view", "view_delegations.view", "view_flags.view", "view_params.view",
            "view_profiles.view", "view_role_promotions.view", "view_roles.view",
            "view_suites.view", "view_templates.view", "view_tenants.view", "view_users.view",
        ];

        var faltan = guardados.Where(g => !grafo.Value.Scopes.Contains(g, StringComparer.OrdinalIgnoreCase)).ToList();
        faltan.Should().BeEmpty($"la siembra debe conceder al administrador todo lo que el aspecto guarda; tiene {grafo.Value.Scopes.Count}");
    }
}
