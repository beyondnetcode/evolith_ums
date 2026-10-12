namespace Evolith.Ums.Application.Test.Authorization.Graph;

using System.Text.Json;
using Xunit;
using FluentAssertions;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Ata la forma del grafo al contrato publicado en
/// <c>src/libs/sdk/contracts/auth-graph.schema.json</c>.
///
/// Existe por G-167: el contrato declaraba `id` obligatorios, nombres bajo
/// `name`/`label` y envoltorios `module`/`resource` que NINGÚN endpoint emitía,
/// y nadie lo notó porque ninguna prueba comparaba ambas cosas. Estas pruebas
/// fallan si alguien vuelve a cambiar la proyección sin actualizar el contrato
/// —o al revés—, que es exactamente el fallo que dejó a los satélites leyendo
/// `undefined` en cada nombre.
/// </summary>
public class AuthGraphPayloadTests
{
    private static AuthorizationGraph Grafo(GraphProjection? proyeccion = null)
    {
        var suiteId = Guid.NewGuid();
        var opcion = new GraphNavigationNode(Guid.NewGuid(), "STOCK_VIEW", "Ver Stock", "Option", 1,
            null, "/inv/stock-view",
            [new GraphNodeAction("VIEW", AccessEffect.Allow, PermissionSource.Template)], []);
        var sub = new GraphNavigationNode(Guid.NewGuid(), "STOCK_OPS", "Operaciones", "SubMenu", 1, null, null, [], [opcion]);
        var menu = new GraphNavigationNode(Guid.NewGuid(), "STOCK", "Stock", "Menu", 1, "package", null, [], [sub]);
        var modulo = new GraphMenuModule(Guid.NewGuid(), "INV", "Inventario", 1, "Active", "package", [menu]);

        var recurso = new GraphDomainPermission(
            Guid.NewGuid(), "Aggregate", "PURCHASE_ORDER", "Orden de Compra",
            Guid.NewGuid(), null,
            new[] { new GraphDomainAction(Guid.NewGuid(), "VIEW", "Ver", AccessEffect.Allow, PermissionSource.Template) });

        var contexto = new GraphContext(
            new GraphUser(Guid.NewGuid(), "ana@evolith.example", "ana", "Ana Torres", "Active"),
            new GraphTenant(Guid.NewGuid(), "EVOLITH", "Evolith UMS", "Active", false),
            new GraphSystemSuite(suiteId, "WMS", "Almacén", "Active"),
            new GraphRole(Guid.NewGuid(), "OPERARIO", "Operario de Almacén", 2, null),
            new GraphProfile(Guid.NewGuid(), "OrgWide", true),
            Branch: null);

        return AuthorizationGraph.Build(
            contexto,
            new GraphAuthentication("Local", null, false, DateTime.UtcNow, DateTime.UtcNow.AddHours(1)),
            new[] { new GraphAction(Guid.NewGuid(), "VIEW", "Ver") },
            new[] { modulo },
            new[] { recurso },
            new[] { new GraphFeatureFlag("WMS_BULK_EXPORT", suiteId, false, null) },
            new GraphEffectiveConfig(60, 5, 12, true, new[] { "Totp" }, 3600000, false),
            new[] { "stock_view.view" },
            DateTime.UtcNow,
            settings: new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["brand"] = new Dictionary<string, string> { ["logo_url"] = "/logo.svg" },
                ["ui"]    = new Dictionary<string, string> { ["home_route"] = "/" },
            },
            projection: proyeccion);
    }

    private static JsonElement Json(GraphSerializationOptions? opts = null) =>
        JsonSerializer.SerializeToElement(AuthGraphPayload.Build(Grafo(), opts));

    /// <summary>
    /// El grafo tal como saldría de <c>?include=...</c>. Se pasa por el PARSER en vez de construir
    /// la proyección a mano: así estas pruebas ejercitan la misma puerta de entrada que el
    /// endpoint, y una regla que solo valiera para proyecciones fabricadas en el test no serviría
    /// de nada.
    /// </summary>
    private static JsonElement JsonProyectado(string include)
    {
        GraphProjection.TryParse(include, out var proyeccion, out var desconocido)
            .Should().BeTrue($"'{include}' debe ser una proyección válida (desconocido: {desconocido})");

        return JsonSerializer.SerializeToElement(AuthGraphPayload.Build(Grafo(proyeccion)));
    }

    private static string[] Claves(JsonElement g) => g.EnumerateObject().Select(p => p.Name).ToArray();

    [Fact]
    public void Nombres_viajan_como_value_no_como_name_ni_label()
    {
        var g = Json();

        g.GetProperty("context").GetProperty("user").GetProperty("value").GetString()
            .Should().Be("Ana Torres");
        g.GetProperty("context").GetProperty("tenant").GetProperty("value").GetString()
            .Should().Be("Evolith UMS");

        var modulo = g.GetProperty("menuAccess")[0];
        modulo.GetProperty("value").GetString().Should().Be("Inventario");
        modulo.TryGetProperty("name", out _).Should().BeFalse();

        var opcion = modulo.GetProperty("nodes")[0].GetProperty("children")[0].GetProperty("children")[0];
        opcion.GetProperty("value").GetString().Should().Be("Ver Stock");
        opcion.TryGetProperty("label", out _).Should().BeFalse();
    }

    [Fact]
    public void Sin_metadatos_tecnicos_no_viaja_ningun_id()
    {
        var g = Json();

        g.GetProperty("context").GetProperty("user").TryGetProperty("id", out _).Should().BeFalse();
        g.GetProperty("menuAccess")[0].TryGetProperty("id", out _).Should().BeFalse();
        g.GetProperty("domainPermissions")[0].TryGetProperty("resourceId", out _).Should().BeFalse();
        g.GetProperty("featureFlags")[0].TryGetProperty("systemSuiteId", out _).Should().BeFalse();
    }

    [Fact]
    public void Con_metadatos_tecnicos_los_ids_aparecen()
    {
        var g = Json(new GraphSerializationOptions(IncludeTechnicalMetadata: true));

        g.GetProperty("context").GetProperty("user").TryGetProperty("id", out _).Should().BeTrue();
        g.GetProperty("menuAccess")[0].TryGetProperty("id", out _).Should().BeTrue();
        g.GetProperty("domainPermissions")[0].TryGetProperty("resourceId", out _).Should().BeTrue();
    }

    [Fact]
    public void MenuAccess_y_domainPermissions_son_listas_planas()
    {
        var g = Json();

        var modulo = g.GetProperty("menuAccess")[0];
        modulo.TryGetProperty("module", out _).Should().BeFalse("el módulo no va envuelto");
        modulo.GetProperty("code").GetString().Should().Be("INV");

        var recurso = g.GetProperty("domainPermissions")[0];
        recurso.TryGetProperty("resource", out _).Should().BeFalse("el recurso no va envuelto");
        recurso.GetProperty("resourceCode").GetString().Should().Be("PURCHASE_ORDER");
        recurso.GetProperty("resourceType").GetString().Should().Be("Aggregate");
        recurso.GetProperty("actions")[0].GetProperty("actionCode").GetString().Should().Be("VIEW");
    }

    [Fact]
    public void SortOrder_viaja_en_los_tres_niveles_de_navegacion()
    {
        var modulo = Json().GetProperty("menuAccess")[0];

        modulo.GetProperty("sortOrder").GetInt32().Should().Be(1);
        var menu = modulo.GetProperty("nodes")[0];
        menu.GetProperty("sortOrder").GetInt32().Should().Be(1);
        menu.GetProperty("children")[0].GetProperty("sortOrder").GetInt32().Should().Be(1);
    }

    [Fact]
    public void EffectiveConfig_publica_los_metodos_mfa_admitidos()
    {
        Json().GetProperty("effectiveConfig").GetProperty("mfaAllowedMethods")
            .EnumerateArray().Select(x => x.GetString()).Should().Equal("Totp");
    }

    [Fact]
    public void Efecto_y_origen_viajan_como_cadena_no_como_ordinal()
    {
        var accion = Json().GetProperty("menuAccess")[0]
            .GetProperty("nodes")[0].GetProperty("children")[0]
            .GetProperty("children")[0].GetProperty("actions")[0];

        accion.GetProperty("effect").GetString().Should().Be("Allow");
        accion.GetProperty("source").GetString().Should().Be("Template");
    }

    [Fact]
    public void Los_nulos_del_grafo_viajan_explicitos()
    {
        var g = Json();

        g.GetProperty("context").GetProperty("branch").ValueKind.Should().Be(JsonValueKind.Null);
        g.GetProperty("authentication").GetProperty("provider").ValueKind.Should().Be(JsonValueKind.Null);
        g.GetProperty("featureFlags")[0].GetProperty("matchedCriteriaType").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void El_icono_y_la_ruta_viajan_en_el_nodo()
    {
        var menu = Json().GetProperty("menuAccess")[0].GetProperty("nodes")[0];

        menu.GetProperty("icon").GetString().Should().Be("package");
        // Un menú agrupa: no navega a ninguna parte.
        menu.GetProperty("route").ValueKind.Should().Be(JsonValueKind.Null);

        var opcion = menu.GetProperty("children")[0].GetProperty("children")[0];
        opcion.GetProperty("route").GetString().Should().Be("/inv/stock-view");
        // Las claves se emiten aunque estén vacías: el cliente no debe distinguir «sin icono»
        // de «clave ausente».
        opcion.GetProperty("icon").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void El_icono_del_modulo_viaja_en_el_modulo()
    {
        // Sin esto el cliente sabe cómo se llama el módulo pero no con qué pintarlo, y acaba
        // resolviendo el icono por código: la tabla estática que el grafo vino a eliminar (G-182).
        Json().GetProperty("menuAccess")[0].GetProperty("icon").GetString().Should().Be("package");
    }

    [Fact]
    public void Los_ajustes_visibles_viajan_agrupados_por_espacio_de_nombres()
    {
        var settings = Json().GetProperty("settings");

        settings.GetProperty("brand").GetProperty("logo_url").GetString().Should().Be("/logo.svg");
        settings.GetProperty("ui").GetProperty("home_route").GetString().Should().Be("/");
    }

    [Fact]
    public void Las_claves_de_primer_nivel_son_las_del_contrato()
    {
        // Igualdad ORDENADA, no «contiene»: es la red que puso G-167, y relajarla a un subconjunto
        // dejaría de proteger contra el defecto original —tres formas distintas del mismo grafo—.
        // Sigue siendo exacta; lo único que cambia es que ahora describe explícitamente el caso
        // SIN proyección, que es el que ningún satélite puede ver alterado.
        Claves(Json()).Should().Equal(
            "schemaVersion", "onboardingPending", "accessState", "context", "authentication",
            "actions", "profiles", "menuAccess", "domainPermissions", "featureFlags",
            "effectiveConfig", "settings", "scopes", "generatedAt", "validUntil");
    }

    // ── Proyección (v2.5.0, `?include=`) ─────────────────────────────────────────

    /// <summary>
    /// Claves que viajan SIEMPRE, con o sin proyección. Sin ellas el grafo no se puede fechar, ni
    /// validar la versión, ni atribuir a nadie: `context` es además un bloqueo duro aguas abajo.
    /// </summary>
    private static readonly string[] Sobre =
    [
        "schemaVersion", "onboardingPending", "accessState", "context", "authentication",
        "generatedAt", "validUntil"
    ];

    [Fact]
    public void Sin_proyeccion_no_se_emite_la_clave_projection()
    {
        // I1 llevada al detalle que importa: no basta con que estén las quince claves; no puede
        // aparecer NINGUNA clave nueva. `additionalProperties: false` en el contrato convierte
        // cualquier añadido en un rechazo para quien valide contra el esquema anterior.
        Json().TryGetProperty("projection", out _).Should().BeFalse(
            "sin `?include=` la respuesta es la de siempre, byte por byte");
    }

    [Theory]
    // permissions es ATÓMICO: un solo token, las dos secciones. No hay caso «solo scopes»
    // porque no hay forma de pedirlo — ver Un_token_por_seccion_suelta_no_existe.
    [InlineData("permissions", new[] { "domainPermissions", "scopes" })]
    [InlineData("navigation",  new[] { "actions", "menuAccess" })]
    [InlineData("profiles",    new[] { "profiles" })]
    [InlineData("features",    new[] { "featureFlags" })]
    [InlineData("config",      new[] { "effectiveConfig" })]
    [InlineData("settings",    new[] { "settings" })]
    public void Cada_proyeccion_devuelve_lo_pedido_y_nada_mas(string include, string[] esperadas)
    {
        var claves = Claves(JsonProyectado(include));

        // Lo pedido está…
        claves.Should().Contain(esperadas);
        // …el sobre también, y el eco…
        claves.Should().Contain(Sobre);
        claves.Should().Contain("projection");
        // …y NADA más. Esta mitad es la que de verdad hace trabajo: sin ella, un productor podría
        // emitir de más y ninguna prueba lo notaría.
        claves.Should().BeEquivalentTo([.. Sobre, "projection", .. esperadas]);
    }

    [Fact]
    public void Un_bloque_no_pedido_se_omite_jamas_llega_vacio()
    {
        // La regla que sostiene todo lo demás. `domainPermissions: []` y «no lo pedí» son
        // indistinguibles para el evaluador de deny-wins: ante los dos concluye que no hay ninguna
        // denegación explícita, y CONCEDE. Si el productor pudiera sustituir la ausencia por un
        // array vacío, la atomicidad quedaría declarada en el contrato y sin defensa observable.
        var g = JsonProyectado("navigation");

        g.TryGetProperty("domainPermissions", out _).Should().BeFalse();
        g.TryGetProperty("scopes", out _).Should().BeFalse();
        g.TryGetProperty("featureFlags", out _).Should().BeFalse();
        g.TryGetProperty("profiles", out _).Should().BeFalse();
        g.TryGetProperty("effectiveConfig", out _).Should().BeFalse();
        g.TryGetProperty("settings", out _).Should().BeFalse();
    }

    [Fact]
    public void Permissions_trae_siempre_las_dos_secciones()
    {
        var g = JsonProyectado("permissions");

        g.GetProperty("scopes").EnumerateArray().Select(x => x.GetString())
            .Should().Equal("stock_view.view");
        g.GetProperty("domainPermissions")[0].GetProperty("resourceCode").GetString()
            .Should().Be("PURCHASE_ORDER");
    }

    [Fact]
    public void Un_token_por_seccion_suelta_no_existe()
    {
        // I2 no la puede garantizar el esquema en la PETICIÓN: la garantiza el vocabulario, porque
        // no hay nada que escribir para pedir una mitad. Cada uno de estos da 400, no media
        // proyección.
        foreach (var suelto in new[] { "scopes", "domainPermissions", "menuAccess", "actions" })
        {
            GraphProjection.TryParse(suelto, out _, out var desconocido).Should().BeFalse(
                $"'{suelto}' es un nombre de SECCIÓN, no un token del vocabulario");
            desconocido.Should().Be(suelto);
        }
    }

    [Fact]
    public void El_sobre_viaja_siempre_con_cualquier_proyeccion()
    {
        foreach (var token in GraphProjection.Vocabulario)
        {
            Claves(JsonProyectado(token)).Should().Contain(Sobre,
                $"el sobre no es negociable, tampoco con `include={token}`");
        }
    }

    [Fact]
    public void El_eco_llega_deduplicado_y_en_orden_canonico()
    {
        // Dos peticiones equivalentes escritas al revés deben producir el MISMO eco: si no, no
        // sirve como parte de una clave de caché, que es la mitad de su razón de ser.
        var directo = JsonProyectado("permissions,navigation");
        var alReves = JsonProyectado(" NAVIGATION , permissions ,, permissions ");

        var esperado = new[] { "permissions", "navigation" };

        directo.GetProperty("projection").EnumerateArray().Select(x => x.GetString())
            .Should().Equal(esperado);
        alReves.GetProperty("projection").EnumerateArray().Select(x => x.GetString())
            .Should().Equal(esperado);
    }

    [Fact]
    public void Varios_tokens_se_acumulan()
    {
        var claves = Claves(JsonProyectado("permissions,features"));

        claves.Should().BeEquivalentTo(
            [.. Sobre, "projection", "domainPermissions", "scopes", "featureFlags"]);
    }
}
