namespace Evolith.Ums.Application.Test.Authorization.Graph;

using System.Text.Json;
using Xunit;
using FluentAssertions;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Ata la PROYECCIÓN que emite el productor al contrato publicado en
/// <c>src/libs/sdk/contracts/auth-graph.schema.json</c>.
///
/// <para>Existe porque en esta solución NO hay ningún validador de JSON Schema: ni en ejecución ni
/// en las pruebas. El esquema es un documento que solo custodian pruebas escritas a mano, y hasta
/// ahora ninguna leía el fichero — comparaban contra una lista copiada, que es exactamente cómo el
/// contrato y los emisores llegaron a divergir en G-167.</para>
///
/// <para>Estas pruebas LEEN el fichero y derivan de él lo que hay que exigir, de modo que cambiar
/// el esquema sin cambiar el productor —o al revés— pone algo en rojo. No sustituyen a un
/// validador 2020-12 de verdad, que sigue haciendo falta y que exigiría una dependencia nueva:
/// cubren la parte del contrato que este cambio introduce, la de qué claves de primer nivel viajan
/// con cada proyección, que es donde un descuadre CONCEDE permisos.</para>
/// </summary>
public class ContratoDeProyeccionTests
{
    private static readonly JsonElement Esquema = CargarEsquema();

    private static JsonElement CargarEsquema()
    {
        // `.git` se comprueba como directorio O como fichero. En un clon normal es un directorio,
        // pero en un WORKTREE de git es un fichero que contiene `gitdir: ...`. Buscando solo el
        // directorio, el bucle no encontraba la raiz, subia hasta la cima del disco y dejaba `dir`
        // en null: el constructor estatico lanzaba y los siete tests de esta clase caian con un
        // TypeInitializationException que no menciona rutas ni worktrees. Medido el 2026-09-01,
        // cuando la suite se corrio desde un worktree para validar una fusion sin tocar el arbol
        // principal --que es justo para lo que sirven los worktrees--.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
               && !Directory.Exists(Path.Combine(dir.FullName, ".git"))
               && !File.Exists(Path.Combine(dir.FullName, ".git")))
            dir = dir.Parent;

        if (dir is null) throw new InvalidOperationException("No se encontró la raíz del repositorio.");

        var ruta = Path.Combine(dir.FullName, "src", "libs", "sdk", "contracts", "auth-graph.schema.json");
        return JsonDocument.Parse(File.ReadAllText(ruta)).RootElement.Clone();
    }

    private static string[] Cadenas(JsonElement array) =>
        array.EnumerateArray().Select(x => x.GetString()!).ToArray();

    /// <summary>La rama del <c>allOf</c> que gobierna las respuestas COMPLETAS.</summary>
    private static JsonElement ReglaSinProyeccion() =>
        Esquema.GetProperty("allOf").EnumerateArray()
            .First(r => r.GetProperty("if").TryGetProperty("not", out _));

    /// <summary>Las seis reglas por token, indexadas por el token que las dispara.</summary>
    private static Dictionary<string, JsonElement> ReglasPorToken()
    {
        var conProyeccion = Esquema.GetProperty("allOf").EnumerateArray()
            .First(r => r.GetProperty("if").TryGetProperty("required", out _));

        return conProyeccion.GetProperty("then").GetProperty("allOf").EnumerateArray()
            .ToDictionary(
                r => r.GetProperty("if").GetProperty("properties").GetProperty("projection")
                      .GetProperty("contains").GetProperty("const").GetString()!,
                r => r);
    }

    private static string[] ClavesDelPayload(GraphProjection? proyeccion) =>
        JsonSerializer.SerializeToElement(AuthGraphPayload.Build(GrafoDePrueba(proyeccion)))
            .EnumerateObject().Select(p => p.Name).ToArray();

    private static AuthorizationGraph GrafoDePrueba(GraphProjection? proyeccion)
    {
        var suiteId = Guid.NewGuid();
        var opcion = new GraphNavigationNode(Guid.NewGuid(), "STOCK_VIEW", "Ver Stock", "Option", 1,
            null, "/inv/stock-view",
            [new GraphNodeAction("VIEW", AccessEffect.Allow, PermissionSource.Template)], []);
        var modulo = new GraphMenuModule(Guid.NewGuid(), "INV", "Inventario", 1, "Active", "package", [opcion]);

        var contexto = new GraphContext(
            new GraphUser(Guid.NewGuid(), "ana@evolith.example", "ana", "Ana Torres", "Active"),
            new GraphTenant(Guid.NewGuid(), "EVOLITH", "Evolith UMS", "Active", false),
            new GraphSystemSuite(suiteId, "WMS", "Almacén", "Active"),
            new GraphRole(Guid.NewGuid(), "OPERARIO", "Operario", 2, null),
            new GraphProfile(Guid.NewGuid(), "OrgWide", true),
            Branch: null);

        return AuthorizationGraph.Build(
            contexto,
            new GraphAuthentication("Local", null, false, DateTime.UtcNow, DateTime.UtcNow.AddHours(1)),
            [new GraphAction(Guid.NewGuid(), "VIEW", "Ver")],
            [modulo],
            [new GraphDomainPermission(Guid.NewGuid(), "Aggregate", "PO", "Orden", null, null,
                [new GraphDomainAction(Guid.NewGuid(), "VIEW", "Ver", AccessEffect.Allow, PermissionSource.Template)])],
            [new GraphFeatureFlag("WMS_BULK", suiteId, false, null)],
            new GraphEffectiveConfig(60, 5, 12, true, ["Totp"], 3600000, false),
            ["stock_view.view"],
            DateTime.UtcNow,
            settings: new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["ui"] = new Dictionary<string, string> { ["home_route"] = "/" },
            },
            projection: proyeccion);
    }

    [Fact]
    public void Las_dos_versiones_del_esquema_publicado_son_las_que_emite_el_productor()
    {
        // Deriva típica y silenciosa: se toca el esquema y no la constante, o al revés. Y no vale
        // confiar en que nadie lo note «porque todos comprueban RANGO»: un satélite con este fichero
        // vendorizado valida contra el `const`, que compara por IGUALDAD.
        Cadenas(Esquema.GetProperty("properties").GetProperty("schemaVersion").GetProperty("enum"))
            .Should().BeEquivalentTo(global::Evolith.Ums.Sdk.Contracts.SchemaVersion.Emitidas);

        // El `$id` lleva la versión MÁS ALTA que el documento describe, que es la proyectada: el
        // fichero describe las dos formas y la 2.5.0 es la que añade.
        Esquema.GetProperty("$id").GetString()
            .Should().EndWith("/" + global::Evolith.Ums.Sdk.Contracts.SchemaVersion.Latest);
    }

    [Fact]
    public void La_respuesta_completa_declara_2_4_0_y_solo_la_proyectada_declara_2_5_0()
    {
        // EL defecto que este arreglo cierra. Subir `schemaVersion` también en las respuestas
        // completas rompía a todo satélite que valide contra un esquema 2.4.0 vendorizado —su
        // `const` compara por igualdad— y lo rompía sin que hubiera enviado jamás `?include=`.
        // La versión la decide la FORMA de la respuesta, no la del código desplegado.
        static string VersionDe(GraphProjection? p) =>
            JsonSerializer.SerializeToElement(AuthGraphPayload.Build(GrafoDePrueba(p)))
                .GetProperty("schemaVersion").GetString()!;

        VersionDe(null).Should().Be(global::Evolith.Ums.Sdk.Contracts.SchemaVersion.Current)
            .And.Be("2.4.0");

        foreach (var token in GraphProjection.Vocabulario)
        {
            GraphProjection.TryParse(token, out var proyeccion, out _).Should().BeTrue();
            VersionDe(proyeccion).Should().Be(global::Evolith.Ums.Sdk.Contracts.SchemaVersion.Projected,
                $"`include={token}` sí cambia la forma de la respuesta");
        }
    }

    [Fact]
    public void Cada_rama_del_contrato_fija_la_version_que_le_toca()
    {
        // Las dos mitades, leídas del fichero: sin proyección el `const` es 2.4.0, con proyección
        // 2.5.0. Es lo que permite que el `enum` de arriba no afloje la comprobación por igualdad:
        // para una respuesta CONCRETA el valor válido sigue siendo uno solo.
        static string ConstDeLaRama(JsonElement rama) =>
            rama.GetProperty("then").GetProperty("properties").GetProperty("schemaVersion")
                .GetProperty("const").GetString()!;

        ConstDeLaRama(ReglaSinProyeccion())
            .Should().Be(global::Evolith.Ums.Sdk.Contracts.SchemaVersion.Current);

        var conProyeccion = Esquema.GetProperty("allOf").EnumerateArray()
            .First(r => r.GetProperty("if").TryGetProperty("required", out _));

        ConstDeLaRama(conProyeccion)
            .Should().Be(global::Evolith.Ums.Sdk.Contracts.SchemaVersion.Projected);
    }

    [Fact]
    public void Sin_proyeccion_el_payload_trae_exactamente_las_claves_que_el_contrato_exige()
    {
        // I1 contra el fichero, no contra una lista copiada aquí. El esquema dice qué exige cuando
        // no hay `projection`; el productor tiene que emitir eso y nada más, porque la raíz declara
        // `additionalProperties: false`.
        var exigidas = Cadenas(ReglaSinProyeccion().GetProperty("then").GetProperty("required"));

        ClavesDelPayload(null).Should().BeEquivalentTo(exigidas);
    }

    [Fact]
    public void El_sobre_del_contrato_es_el_que_no_se_puede_proyectar()
    {
        // El `required` de la raíz aplica pase lo que pase: es el sobre. Ninguna proyección puede
        // dejar fuera ninguna de sus claves (I4).
        var sobre = Cadenas(Esquema.GetProperty("required"));

        foreach (var token in GraphProjection.Vocabulario)
        {
            GraphProjection.TryParse(token, out var proyeccion, out _).Should().BeTrue();
            ClavesDelPayload(proyeccion).Should().Contain(sobre,
                $"el sobre viaja siempre, también con `include={token}`");
        }
    }

    [Fact]
    public void Cada_token_emite_las_secciones_que_el_contrato_le_asigna_y_ninguna_mas()
    {
        var sobre  = Cadenas(Esquema.GetProperty("required"));
        var reglas = ReglasPorToken();

        // El vocabulario del código y el del esquema tienen que ser el MISMO conjunto: si el
        // esquema declarase un token que el parser no acepta, un cliente lo leería como válido y
        // recibiría un 400.
        var vocabularioDelEsquema = Cadenas(
            Esquema.GetProperty("properties").GetProperty("projection").GetProperty("items").GetProperty("enum"));
        vocabularioDelEsquema.Should().BeEquivalentTo(GraphProjection.Vocabulario);
        reglas.Keys.Should().BeEquivalentTo(GraphProjection.Vocabulario);

        foreach (var (token, regla) in reglas)
        {
            var secciones = Cadenas(regla.GetProperty("then").GetProperty("required"));

            GraphProjection.TryParse(token, out var proyeccion, out _).Should().BeTrue();

            ClavesDelPayload(proyeccion).Should().BeEquivalentTo(
                [.. sobre, "projection", .. secciones],
                $"`include={token}` debe traer el sobre, el eco y exactamente {string.Join("+", secciones)}");
        }
    }

    [Fact]
    public void El_contrato_prohibe_emitir_un_bloque_no_pedido_aunque_sea_vacio()
    {
        // La mitad que se olvida. Sin la rama `else`, un productor podría emitir
        // `domainPermissions: []` en vez de omitirlo y el esquema lo aceptaría — y ningún
        // consumidor podría distinguir ese vacío de una lista sin denegaciones. Se comprueba que
        // la prohibición existe y que cubre EXACTAMENTE las mismas secciones que el `then` exige.
        foreach (var (token, regla) in ReglasPorToken())
        {
            var exigidas = Cadenas(regla.GetProperty("then").GetProperty("required"));
            var prohibidas = ProhibidasPorElElse(regla);

            prohibidas.Should().BeEquivalentTo(exigidas,
                $"si `{token}` no se pide, sus secciones deben estar AUSENTES, no vacías");
        }
    }

    /// <summary>
    /// Secciones que la rama <c>else</c> declara prohibidas. El esquema usa <c>not.required</c>
    /// para una sola sección y <c>not.anyOf[].required</c> para varias; se admiten las dos formas
    /// porque son la misma regla escrita como toca en cada caso.
    /// </summary>
    private static string[] ProhibidasPorElElse(JsonElement regla)
    {
        var negado = regla.GetProperty("else").GetProperty("not");

        if (negado.TryGetProperty("anyOf", out var alternativas))
        {
            return alternativas.EnumerateArray()
                .SelectMany(a => Cadenas(a.GetProperty("required")))
                .ToArray();
        }

        return Cadenas(negado.GetProperty("required"));
    }

    [Fact]
    public void El_par_atomico_permissions_lo_declara_tambien_el_contrato()
    {
        // I2 expresada donde puede comprobarla un tercero, no solo en el parser. El esquema no
        // puede impedir que alguien PIDA una mitad —eso lo impide el vocabulario— pero sí puede
        // rechazar una respuesta que traiga una sin la otra.
        Cadenas(ReglasPorToken()["permissions"].GetProperty("then").GetProperty("required"))
            .Should().BeEquivalentTo("scopes", "domainPermissions");
    }
}
