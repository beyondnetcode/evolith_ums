using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Security;

/// <summary>
/// <c>?include=</c> en <c>GET /api/v1/client/graph</c> (contrato 2.5.0), contra la API real.
///
/// <para>El grafo real está dominado por un solo bloque —la navegación es la inmensa mayoría del
/// payload— y no todo satélite necesita todo. Pero recortar un grafo de autorización es peligroso
/// por naturaleza: quien lo recibe decide permisos con lo que tiene, y una sección que falta se
/// parece muchísimo a una sección vacía. Estas pruebas fijan las cuatro condiciones que hacen que
/// el recorte sea seguro:</para>
///
/// <list type="number">
///   <item>sin parámetro, la respuesta de siempre — ningún satélite existente se entera;</item>
///   <item>cada proyección trae lo pedido y NADA más, y lo no pedido se OMITE, no llega vacío;</item>
///   <item><c>permissions</c> es atómico: nunca llega una mitad sin la otra;</item>
///   <item>un valor desconocido es 400 con un mensaje accionable, jamás un silencio.</item>
/// </list>
/// </summary>
public sealed class ProyeccionDelGrafoTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private const string InquilinoCliente = "COMEX_ANDINA";
    private const string UsuarioCliente   = "usuario.impo@comexandina.com.pe";

    /// <summary>Claves que viajan siempre, con o sin proyección (el «sobre»).</summary>
    private static readonly string[] Sobre =
    [
        "schemaVersion", "onboardingPending", "accessState", "context", "authentication",
        "generatedAt", "validUntil"
    ];

    private static readonly string[] ContratoCompleto =
    [
        "schemaVersion", "onboardingPending", "accessState", "context", "authentication",
        "actions", "profiles", "menuAccess", "domainPermissions", "featureFlags",
        "effectiveConfig", "settings", "scopes", "generatedAt", "validUntil"
    ];

    private readonly UmsApiWebApplicationFactory _factory;

    public ProyeccionDelGrafoTests(UmsApiWebApplicationFactory factory) => _factory = factory;

    // ── I1: sin parámetro, todo ──────────────────────────────────────────────────

    [Fact]
    public async Task Sin_include_el_grafo_llega_completo_y_sin_marca_de_proyeccion()
    {
        var (cliente, ct) = await ClienteAutenticadoAsync();

        var g = await GrafoAsync(cliente, "/api/v1/client/graph", ct);

        Claves(g).Should().BeEquivalentTo(ContratoCompleto,
            "omitir `include` debe devolver exactamente el mismo grafo de siempre");
        g.TryGetProperty("projection", out _).Should().BeFalse(
            "una clave nueva en la respuesta completa rompería a quien valide contra el esquema anterior");

        // Y el token de versión tampoco se mueve. Es la parte que más fácil se cuela como «da
        // igual»: un satélite con el esquema 2.4.0 vendorizado valida `schemaVersion` contra un
        // `const`, que compara por IGUALDAD, y un 2.5.0 le hace rechazar TODAS las respuestas sin
        // haber pedido nunca una proyección.
        g.GetProperty("schemaVersion").GetString()
            .Should().Be(global::Evolith.Ums.Sdk.Contracts.SchemaVersion.Current)
            .And.Be("2.4.0");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",")]
    public async Task Un_include_sin_valor_util_se_trata_como_ausente(string include)
    {
        // Entre leer una cadena vacía como «todo» o como «solo el sobre», solo la primera es
        // segura: dar de más nunca rompe a un cliente, dar de menos sí.
        var (cliente, ct) = await ClienteAutenticadoAsync();

        var g = await GrafoAsync(cliente, $"/api/v1/client/graph?include={Uri.EscapeDataString(include)}", ct);

        Claves(g).Should().BeEquivalentTo(ContratoCompleto);
        g.TryGetProperty("projection", out _).Should().BeFalse();
    }

    // ── I4 + «lo pedido y nada más» ──────────────────────────────────────────────

    [Theory]
    [InlineData("permissions", new[] { "domainPermissions", "scopes" })]
    [InlineData("navigation",  new[] { "actions", "menuAccess" })]
    [InlineData("profiles",    new[] { "profiles" })]
    [InlineData("features",    new[] { "featureFlags" })]
    [InlineData("config",      new[] { "effectiveConfig" })]
    [InlineData("settings",    new[] { "settings" })]
    public async Task Cada_proyeccion_trae_el_sobre_el_eco_y_solo_lo_pedido(string include, string[] esperadas)
    {
        var (cliente, ct) = await ClienteAutenticadoAsync();

        var g = await GrafoAsync(cliente, $"/api/v1/client/graph?include={include}", ct);

        Claves(g).Should().BeEquivalentTo([.. Sobre, "projection", .. esperadas]);

        g.GetProperty("projection").EnumerateArray().Select(x => x.GetString())
            .Should().Equal(include);

        // La versión sube AQUÍ y solo aquí: esta respuesta sí tiene una forma nueva —le faltan
        // bloques y trae el eco— y quien la recibe la pidió.
        g.GetProperty("schemaVersion").GetString()
            .Should().Be(global::Evolith.Ums.Sdk.Contracts.SchemaVersion.Projected)
            .And.Be("2.5.0");
    }

    [Fact]
    public async Task Lo_no_pedido_se_omite_jamas_llega_vacio()
    {
        // La regla que sostiene todo lo demás. `domainPermissions: []` y «no lo pedí» son
        // indistinguibles para el evaluador de deny-wins: ante ambos concluye que no hay ninguna
        // denegación explícita y CONCEDE. Emitir el bloque vacío dejaría la atomicidad declarada en
        // el contrato y sin ninguna defensa observable.
        var (cliente, ct) = await ClienteAutenticadoAsync();

        var g = await GrafoAsync(cliente, "/api/v1/client/graph?include=navigation", ct);

        foreach (var ausente in new[]
                 { "domainPermissions", "scopes", "featureFlags", "profiles", "effectiveConfig", "settings" })
        {
            g.TryGetProperty(ausente, out _).Should().BeFalse(
                $"'{ausente}' no se pidió: debe estar AUSENTE, no vacío");
        }
    }

    [Fact]
    public async Task El_sobre_viaja_con_cualquier_proyeccion()
    {
        var (cliente, ct) = await ClienteAutenticadoAsync();

        foreach (var token in new[] { "permissions", "navigation", "profiles", "features", "config", "settings" })
        {
            var g = await GrafoAsync(cliente, $"/api/v1/client/graph?include={token}", ct);

            Claves(g).Should().Contain(Sobre, $"el sobre no es negociable, tampoco con `include={token}`");

            // `context.user` y `context.tenant` son bloqueo duro aguas abajo: un grafo sin ellos no
            // produce una app con menos opciones, produce un 502 y nadie entra.
            g.GetProperty("context").GetProperty("user").TryGetProperty("email", out _).Should().BeTrue();
            g.GetProperty("context").GetProperty("tenant").GetProperty("code").GetString()
                .Should().Be(InquilinoCliente);
        }
    }

    // ── I2: atomicidad ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Permissions_trae_siempre_las_dos_secciones()
    {
        var (cliente, ct) = await ClienteAutenticadoAsync();

        var g = await GrafoAsync(cliente, "/api/v1/client/graph?include=permissions", ct);

        g.TryGetProperty("scopes", out var scopes).Should().BeTrue();
        g.TryGetProperty("domainPermissions", out var dominio).Should().BeTrue();

        scopes.ValueKind.Should().Be(JsonValueKind.Array);
        dominio.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Theory]
    [InlineData("scopes")]
    [InlineData("domainPermissions")]
    public async Task No_hay_forma_de_pedir_media_proyeccion_de_permisos(string mitad)
    {
        // El vocabulario no tiene token para ninguna de las dos mitades, así que pedir una es un
        // valor desconocido — 400, no media proyección. Es la única defensa de verdad contra un
        // grafo con scopes y sin las denegaciones explícitas de dominio.
        var (cliente, ct) = await ClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync($"/api/v1/client/graph?include={mitad}", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var error = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        error.RootElement.GetProperty("message").GetString().Should().Contain("permissions",
            "el mensaje debe encaminar al token que SÍ trae esa sección");
    }

    // ── I3: valor desconocido → 400 ──────────────────────────────────────────────

    [Fact]
    public async Task Un_include_desconocido_devuelve_400_y_dice_que_valor_y_cuales_valen()
    {
        // El caso del enunciado: la errata. Si el servidor la ignorase, el satélite creería haber
        // pedido los permisos y recibiría un grafo sin ellos — y leería esa ausencia como «no
        // tengo permisos» o, peor, se saltaría la comprobación. Un error al integrar vale más que
        // una degradación muda en producción.
        var (cliente, ct) = await ClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/v1/client/graph?include=permisions", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var error = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        var raiz = error.RootElement;

        // El grupo `/api/v1/client` usa `ClientAuthErrorResponse`, no ProblemDetails: mezclarlos
        // dejaría a los satélites con dos formas de error en la misma superficie.
        raiz.GetProperty("code").GetString().Should().Be("AUTH_001");
        raiz.GetProperty("supportReferenceId").GetString().Should().NotBeNullOrWhiteSpace();

        var mensaje = raiz.GetProperty("message").GetString()!;
        mensaje.Should().Contain("permisions", "hay que decir QUÉ valor no se reconoce");
        foreach (var valido in new[] { "permissions", "navigation", "profiles", "features", "config", "settings" })
            mensaje.Should().Contain(valido, "hay que decir CUÁLES son válidos");
    }

    [Fact]
    public async Task Un_valor_desconocido_entre_validos_tambien_es_400()
    {
        // No se aplica «lo que se entienda»: una proyección a medias es peor que ninguna, porque
        // el cliente creería que se aplicó entera.
        var (cliente, ct) = await ClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync(
            "/api/v1/client/graph?include=permissions,navegacion,features", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync(ct)).Should().Contain("navegacion");
    }

    [Fact]
    public async Task El_400_no_depende_de_construir_el_grafo_pero_si_de_estar_autenticado()
    {
        // Orden de las guardas: la autenticación primero. Un `include` inválido no puede ser una
        // forma de averiguar si este endpoint existe sin presentar credenciales.
        var cliente = CrearCliente();

        var respuesta = await cliente.GetAsync(
            "/api/v1/client/graph?include=permisions", TestContext.Current.CancellationToken);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Forma del parámetro ──────────────────────────────────────────────────────

    [Fact]
    public async Task El_parametro_tolera_mayusculas_espacios_y_duplicados()
    {
        var (cliente, ct) = await ClienteAutenticadoAsync();

        var g = await GrafoAsync(cliente,
            "/api/v1/client/graph?include=" + Uri.EscapeDataString(" NAVIGATION , permissions ,, permissions "), ct);

        // El eco llega deduplicado y en el orden canónico del vocabulario: dos peticiones
        // equivalentes escritas al revés deben producir el mismo eco, o no serviría como parte de
        // una clave de caché.
        g.GetProperty("projection").EnumerateArray().Select(x => x.GetString())
            .Should().Equal("permissions", "navigation");
    }

    [Fact]
    public async Task La_proyeccion_es_estable_entre_dos_llamadas()
    {
        // El grafo se RECONSTRUYE en cada petición. Dos revalidaciones seguidas con la misma
        // proyección tienen que dar la misma forma: si no, un satélite vería cambiar sus permisos
        // a mitad de sesión y el síntoma aparecería lejos de la causa.
        var (cliente, ct) = await ClienteAutenticadoAsync();

        var primera = Claves(await GrafoAsync(cliente, "/api/v1/client/graph?include=permissions", ct));
        var segunda = Claves(await GrafoAsync(cliente, "/api/v1/client/graph?include=permissions", ct));

        segunda.Should().BeEquivalentTo(primera);
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────────

    private static string[] Claves(JsonElement g) => g.EnumerateObject().Select(p => p.Name).ToArray();

    private HttpClient CrearCliente() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress       = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies     = false,
    });

    private async Task<(HttpClient Cliente, CancellationToken Ct)> ClienteAutenticadoAsync()
    {
        var ct      = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/client/authenticate", new
        {
            tenantCode = InquilinoCliente,
            username   = UsuarioCliente,
            password   = CoreDevDataSeeder.EffectiveEvolithDevPassword,
        }, ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));

        cliente.DefaultRequestHeaders.Authorization =
            new("Bearer", payload.RootElement.GetProperty("token").GetString());

        return (cliente, ct);
    }

    private static async Task<JsonElement> GrafoAsync(HttpClient cliente, string url, CancellationToken ct)
    {
        var respuesta = await cliente.GetAsync(url, ct);
        var cuerpo    = await respuesta.Content.ReadAsStringAsync(ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Cuerpo: {cuerpo}");

        return JsonDocument.Parse(cuerpo).RootElement.Clone();
    }
}
