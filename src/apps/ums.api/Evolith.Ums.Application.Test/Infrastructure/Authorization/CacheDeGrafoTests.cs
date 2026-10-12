using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Infrastructure.Authorization.Graph;
using Xunit;

namespace Evolith.Ums.Application.Test.Infrastructure.Authorization;

/// <summary>
/// La retención del grafo, sobre la implementación EN MEMORIA de `IDistributedCache` — que es la
/// que UMS resuelve por omisión cuando no hay `Redis:Connection`. La misma clase sirve las dos
/// posturas: lo que cambia entre memoria y Redis es dónde vive el dato, no este código.
/// </summary>
public sealed class CacheDeGrafoTests
{
    /// <summary>El inquilino de la mayoría de las pruebas. Fijo, para que solo varíe lo medido.</summary>
    private static readonly Guid Inquilino = Guid.NewGuid();

    private static CacheDeGrafoDistribuida Sujeto(IDictionary<string, string?>? ajustes = null)
    {
        var cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(ajustes ?? new Dictionary<string, string?>()).Build();
        return new CacheDeGrafoDistribuida(cache, config);
    }

    private static AuthorizationGraph Grafo(params string[] scopes)
    {
        var suiteId = Guid.NewGuid();
        var opcion = new GraphNavigationNode(Guid.NewGuid(), "USERS_VIEW", "Ver Usuarios", "Option", 1,
            null, "/users",
            [new GraphNodeAction("VIEW", AccessEffect.Allow, PermissionSource.Template)], []);
        var modulo = new GraphMenuModule(Guid.NewGuid(), "IDM", "Identidad", 1, "Active", "users", [opcion]);

        var contexto = new GraphContext(
            new GraphUser(Guid.NewGuid(), "ana@evolith.example", "ana", "Ana Torres", "Active"),
            new GraphTenant(Guid.NewGuid(), "EVOLITH", "Evolith UMS", "Active", true),
            new GraphSystemSuite(suiteId, "UMS", "Gestion de Usuarios", "Active"),
            new GraphRole(Guid.NewGuid(), "ADMIN_UMS", "Administrador", 1, null),
            new GraphProfile(Guid.NewGuid(), "OrgWide", true),
            Branch: null);

        return AuthorizationGraph.Build(
            contexto,
            new GraphAuthentication("Local", null, false, DateTime.UtcNow, DateTime.UtcNow.AddHours(1)),
            [new GraphAction(Guid.NewGuid(), "VIEW", "Ver")],
            [modulo],
            [],
            [],
            new GraphEffectiveConfig(60, 5, 12, true, ["Totp"], 3600000, false),
            scopes,
            DateTime.UtcNow);
    }

    [Fact]
    public async Task Lo_guardado_se_recupera_igual()
    {
        var sujeto = Sujeto();
        var perfil = Guid.NewGuid();
        var clave = await sujeto.ClaveAsync(Inquilino, perfil, "UMS", null);
        await sujeto.GuardarAsync(clave, Grafo("view_users.view", "manage_users.manage"));

        var recuperado = await sujeto.ObtenerAsync(clave);

        recuperado.Should().NotBeNull();
        // Se comprueba sobre el JSON emitido, que es lo que la cache retiene: el mismo payload que
        // viaja al satelite. Los scopes viajan en minusculas, deduplicados y ordenados.
        using var doc = System.Text.Json.JsonDocument.Parse(recuperado!);
        doc.RootElement.GetProperty("scopes").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo("view_users.view", "manage_users.manage");
    }

    [Fact]
    public async Task Una_sesion_sin_grafo_devuelve_nulo()
    {
        var s = Sujeto();
        (await s.ObtenerAsync(await s.ClaveAsync(Inquilino, Guid.NewGuid(), "UMS", null))).Should().BeNull();
    }

    [Fact]
    public async Task Invalidar_descarta_lo_retenido()
    {
        // La mitad que convierte una caché en un agujero si falta: sin esto, un permiso revocado
        // seguiría concediendo hasta que el grafo caducase solo.
        var sujeto = Sujeto();
        var perfil = Guid.NewGuid();
        var antes = await sujeto.ClaveAsync(Inquilino, perfil, "UMS", null);
        await sujeto.GuardarAsync(antes, Grafo("manage_users.manage"));

        await sujeto.InvalidarPerfilAsync(perfil);

        // La clave CAMBIA al avanzar la version: lo retenido queda inalcanzable de golpe, que es
        // como se invalidan todas las variantes de un perfil sin poder borrar por prefijo.
        var despues = await sujeto.ClaveAsync(Inquilino, perfil, "UMS", null);
        despues.Should().NotBe(antes);
        (await sujeto.ObtenerAsync(despues)).Should().BeNull();
    }

    [Fact]
    public async Task Guardar_dos_veces_deja_el_ultimo()
    {
        var sujeto = Sujeto();
        var clave = await sujeto.ClaveAsync(Inquilino, Guid.NewGuid(), "UMS", null);
        await sujeto.GuardarAsync(clave, Grafo("view_users.view"));
        await sujeto.GuardarAsync(clave, Grafo("manage_users.manage"));

        using var doc = System.Text.Json.JsonDocument.Parse((await sujeto.ObtenerAsync(clave))!);
        doc.RootElement.GetProperty("scopes").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo("manage_users.manage");
    }

    [Fact]
    public async Task Una_clave_vacia_no_revienta_ni_retiene()
    {
        var sujeto = Sujeto();
        await sujeto.GuardarAsync("", Grafo("view_users.view"));
        (await sujeto.ObtenerAsync("")).Should().BeNull();
    }

    [Fact]
    public async Task La_vida_del_grafo_se_configura()
    {
        // No se comprueba la caducidad por reloj --seria una prueba lenta y fragil-- sino que la
        // clave se lee: con un valor invalido cae al valor por omision y sigue funcionando.
        var sujeto = Sujeto(new Dictionary<string, string?>
        {
            [CacheDeGrafoDistribuida.ClaveVida] = "no-es-un-numero",
        });

        var clave = await sujeto.ClaveAsync(Inquilino, Guid.NewGuid(), "UMS", null);
        await sujeto.GuardarAsync(clave, Grafo("view_users.view"));

        (await sujeto.ObtenerAsync(clave)).Should().NotBeNull();
    }
[Fact]
    public async Task Sistemas_distintos_no_comparten_grafo()
    {
        // El parametro include y el sistema pedido producen grafos DISTINTOS: include recorta
        // secciones enteras, asi que servir uno por otro daria un grafo SIN domainPermissions que
        // pasaria por completo, concediendo en silencio lo que UMS habia denegado.
        var sujeto = Sujeto();
        var perfil = Guid.NewGuid();

        var ums = await sujeto.ClaveAsync(Inquilino, perfil, "UMS", null);
        var sdlc = await sujeto.ClaveAsync(Inquilino, perfil, "SDLC", null);
        var proyectado = await sujeto.ClaveAsync(Inquilino, perfil, "UMS", "permissions,navigation");

        ums.Should().NotBe(sdlc);
        ums.Should().NotBe(proyectado);
    }

    [Fact]
    public async Task Invalidar_un_perfil_no_toca_a_otro()
    {
        var sujeto = Sujeto();
        var mio = Guid.NewGuid();
        var ajeno = Guid.NewGuid();
        var claveAjena = await sujeto.ClaveAsync(Inquilino, ajeno, "UMS", null);
        await sujeto.GuardarAsync(claveAjena, Grafo("view_users.view"));

        await sujeto.InvalidarPerfilAsync(mio);

        (await sujeto.ObtenerAsync(await sujeto.ClaveAsync(Inquilino, ajeno, "UMS", null))).Should().NotBeNull();
    }

    [Fact]
    public async Task Lo_retenido_usa_la_forma_del_cable_y_no_la_del_serializador_por_omision()
    {
        // Lo retenido se DEVUELVE TAL CUAL, sin volver a serializar, así que estos bytes son la
        // respuesta. `AuthGraphPayload` no lleva ni un `[JsonPropertyName]`: su forma la decide
        // entera el serializador, y `JsonSerializer.Serialize` sin opciones da PascalCase mientras
        // las APIs mínimas emiten camelCase. Servir `Context` donde el satélite busca `context` no
        // falla — simplemente no encuentra nada, y todo queda denegado en silencio.
        var sujeto = Sujeto();
        var clave = await sujeto.ClaveAsync(Inquilino, Guid.NewGuid(), "UMS", null);
        await sujeto.GuardarAsync(clave, Grafo("view_users.view"));

        var json = await sujeto.ObtenerAsync(clave);

        json.Should().Contain("\"context\"", "es la forma que emite el endpoint");
        json.Should().NotContain("\"Context\"", "PascalCase dejaría al satélite sin las claves que busca");
    }

    [Fact]
    public async Task Invalidar_un_inquilino_alcanza_a_todos_sus_perfiles()
    {
        // Es la razón de ser de este nivel: tocar un rol, una plantilla o una suite cambia el grafo
        // de TODOS los perfiles del inquilino, y enumerarlos uno a uno en cada manejador es donde
        // esto se rompe en silencio.
        var sujeto = Sujeto();
        var uno = Guid.NewGuid();
        var otro = Guid.NewGuid();

        await sujeto.GuardarAsync(await sujeto.ClaveAsync(Inquilino, uno, "UMS", null), Grafo("view_users.view"));
        await sujeto.GuardarAsync(await sujeto.ClaveAsync(Inquilino, otro, "UMS", null), Grafo("view_users.view"));

        await sujeto.InvalidarInquilinoAsync(Inquilino);

        (await sujeto.ObtenerAsync(await sujeto.ClaveAsync(Inquilino, uno, "UMS", null))).Should().BeNull();
        (await sujeto.ObtenerAsync(await sujeto.ClaveAsync(Inquilino, otro, "UMS", null))).Should().BeNull();
    }

    [Fact]
    public async Task Invalidar_un_inquilino_no_toca_a_otro()
    {
        // El aislamiento es lo que hace que este nivel valga la pena en vez del global: todo el
        // modelo de autorización lleva TenantId y `Profile` rechaza una plantilla ajena, así que un
        // cambio en EVOLITH no puede alterar el grafo de otro cliente.
        var sujeto = Sujeto();
        var otroInquilino = Guid.NewGuid();
        var perfil = Guid.NewGuid();

        var claveAjena = await sujeto.ClaveAsync(otroInquilino, perfil, "UMS", null);
        await sujeto.GuardarAsync(claveAjena, Grafo("view_users.view"));

        await sujeto.InvalidarInquilinoAsync(Inquilino);

        (await sujeto.ObtenerAsync(await sujeto.ClaveAsync(otroInquilino, perfil, "UMS", null)))
            .Should().NotBeNull("un cambio en un inquilino no puede tirar los grafos de otro");
    }

    [Fact]
    public async Task Invalidar_global_alcanza_a_todos_los_inquilinos()
    {
        // Hace falta porque `FeatureFlagProps.TenantId` y `AppConfiguration.TenantId` son ANULABLES:
        // una bandera o un parámetro sin inquilino cambian el grafo de todos, y el contador por
        // inquilino no se enteraría.
        var sujeto = Sujeto();
        var otroInquilino = Guid.NewGuid();
        var perfil = Guid.NewGuid();

        await sujeto.GuardarAsync(await sujeto.ClaveAsync(Inquilino, perfil, "UMS", null), Grafo("view_users.view"));
        await sujeto.GuardarAsync(await sujeto.ClaveAsync(otroInquilino, perfil, "UMS", null), Grafo("view_users.view"));

        await sujeto.InvalidarGlobalAsync();

        (await sujeto.ObtenerAsync(await sujeto.ClaveAsync(Inquilino, perfil, "UMS", null))).Should().BeNull();
        (await sujeto.ObtenerAsync(await sujeto.ClaveAsync(otroInquilino, perfil, "UMS", null))).Should().BeNull();
    }

    [Fact]
    public async Task Sin_inquilino_la_clave_sigue_siendo_estable_y_la_global_la_alcanza()
    {
        // El administrador interno opera entre inquilinos y `OrganizationId` es null. Su grafo se
        // retiene igual, y lo alcanza el contador global —que es donde el behavior lo manda—.
        var sujeto = Sujeto();
        var perfil = Guid.NewGuid();

        var clave = await sujeto.ClaveAsync(null, perfil, "UMS", null);
        (await sujeto.ClaveAsync(null, perfil, "UMS", null)).Should().Be(clave);

        await sujeto.GuardarAsync(clave, Grafo("view_users.view"));
        await sujeto.InvalidarGlobalAsync();

        (await sujeto.ObtenerAsync(await sujeto.ClaveAsync(null, perfil, "UMS", null))).Should().BeNull();
    }
}