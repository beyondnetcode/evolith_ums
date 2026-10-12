using System.IdentityModel.Tokens.Jwt;
using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Security;

/// <summary>
/// G-295 y G-297 — renovar una sesión no puede perder lo que el ingreso ya sabía.
///
/// <para>Son dos fichas y un mismo síntoma. [[G-247]] cerró el logout con una garantía nombrada: el
/// ingreso acuña una sesión (<c>sid</c>) que viaja en los DOS portadores —cookie y JWT—, el logout la
/// anota y <c>TokenRevocationMiddleware</c> la consulta en cada petición. Esa garantía se sostiene
/// mientras el <c>sid</c> sea el MISMO durante toda la vida de la sesión, y no lo era: de los cinco
/// emisores de portador solo el ingreso pasaba la sesión, así que bastaba UNA renovación —que el
/// cliente hace solo, por diseño— para que el portador vivo llevara un <c>sid</c> que el logout no
/// conoce. <c>POST /auth/logout</c> respondía 200 y no cerraba nada.</para>
///
/// <para>La otra mitad es el sistema: el ingreso admite <c>systemCode</c> y el portal lo envía, pero
/// los dos caminos de refresco rearmaban el grafo por desempate, de modo que un sujeto con perfil en
/// varios sistemas podía entrar en uno y salir de la renovación en otro.</para>
///
/// <para>Estas pruebas miran el COMPORTAMIENTO —qué abre la puerta y qué grafo vuelve—, no la
/// implementación: es lo que faltaba, porque un logout que responde 200 sin cerrar nada es
/// indistinguible de uno correcto si solo se mira el código de estado.</para>
/// </summary>
public sealed class LaSesionSobreviveAlRefrescoTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private const string Inquilino = "EVOLITH";
    private const string Usuario   = "admin@evolith.example";

    private readonly UmsApiWebApplicationFactory _factory;

    public LaSesionSobreviveAlRefrescoTests(UmsApiWebApplicationFactory factory) => _factory = factory;

    // ── G-295: la sesión sobrevive al refresco ──────────────────────────────────

    [Fact]
    public async Task El_Portador_Refrescado_Conserva_La_Sesion_Del_Ingreso()
    {
        var ct = TestContext.Current.CancellationToken;
        var (cookie, portadorDelIngreso, _) = await IngresarAsync(ct: ct);

        var refrescado = await RefrescarAsync(cookie, ct);

        Claim(refrescado, "sid").Should().Be(Claim(portadorDelIngreso, "sid"),
            because: "renovar es continuar la MISMA sesión; un `sid` nuevo la vuelve inalcanzable para el logout (G-295)");
        refrescado.Should().NotBe(portadorDelIngreso,
            because: "aun así el portador es otro: si fuera el mismo, esta prueba pasaría sin probar nada");
    }

    /// <summary>
    /// La prueba que G-295 echa en falta, y la única que distingue un logout que cierra de uno que
    /// solo responde 200: refrescar, cerrar sesión y comprobar que el portador REFRESCADO ya no
    /// sirve. Antes seguía siendo criptográficamente válido hasta caducar.
    /// </summary>
    [Fact]
    public async Task Cerrar_Sesion_Alcanza_Al_Portador_Ya_Refrescado()
    {
        var ct = TestContext.Current.CancellationToken;
        var (cookie, _, _) = await IngresarAsync(ct: ct);

        var refrescado = await RefrescarAsync(cookie, ct);

        // Punto de partida: el portador refrescado abre la puerta. Sin esto, el 401 de más abajo
        // podría deberse a que nunca sirvió.
        (await ClienteConPortador(refrescado).GetAsync("/api/v1/auth/session", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await ClienteConCookie(cookie).PostAsync("/api/v1/auth/logout", content: null, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await ClienteConPortador(refrescado).GetAsync("/api/v1/auth/session", ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                because: "cerrar sesión cierra el DISPOSITIVO, y el portador refrescado es el mismo dispositivo (G-247/G-295)");
    }

    [Fact]
    public async Task El_Cambio_De_Perfil_Conserva_La_Sesion_Del_Portador()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, portadorDelIngreso, ingreso) = await IngresarAsync(ct: ct);

        var perfil = ingreso.GetProperty("profileId").GetString();
        perfil.Should().NotBeNullOrWhiteSpace(
            because: "el usuario de prueba debe tener perfil para que este camino sea ejercitable");

        // Se cambia al MISMO perfil: lo que se prueba es qué conserva el camino, no a dónde lleva.
        // Un segundo perfil no añadiría nada aquí y ataría la prueba a la forma de la semilla.
        var cliente = ClienteConPortador(portadorDelIngreso);
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/auth/switch-profile", new { profileId = perfil }, ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await respuesta.Content.ReadAsStringAsync(ct));

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        var portadorNuevo = cuerpo.RootElement.GetProperty("token").GetString()!;

        Claim(portadorNuevo, "sid").Should().Be(Claim(portadorDelIngreso, "sid"),
            because: "cambiar de sombrero no abre una sesión distinta: la identidad y el dispositivo son los mismos (G-295)");
    }

    // ── G-297: el sistema pedido sobrevive a la renovación ──────────────────────

    /// <summary>
    /// El ingreso admite <c>systemCode</c> y el portal lo envía; la renovación lo perdía y rearmaba
    /// el grafo por desempate. Se mira el ECO del grafo (<c>context.requestedSystem</c>) y no el
    /// <c>sys_suite</c> resuelto, porque son dos hechos distintos: con un solo sistema sembrado, el
    /// desempate acierta por casualidad y el <c>sys_suite</c> no distinguiría el arreglo del defecto.
    /// </summary>
    [Fact]
    public async Task La_Renovacion_Conserva_El_Sistema_Pedido_En_El_Ingreso()
    {
        var ct = TestContext.Current.CancellationToken;
        var (cookie, _, ingreso) = await IngresarAsync(sistema: "UMS", ct: ct);

        SistemaPedidoDelGrafo(ingreso).Should().Be("UMS",
            because: "el ingreso debe hacerse eco del sistema que se le pidió, o no hay nada que conservar");

        var cliente = ClienteConCookie(cookie);
        var respuesta = await cliente.PostAsync("/api/v1/auth/refresh", content: null, ct);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await respuesta.Content.ReadAsStringAsync(ct));

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        SistemaPedidoDelGrafo(cuerpo.RootElement).Should().Be("UMS",
            because: "renovar no es volver a elegir sistema: el grafo debe rearmarse sobre el que pidió el ingreso (G-297)");

        var portador = cuerpo.RootElement.GetProperty("token").GetString()!;
        Claim(portador, "req_sys").Should().Be("UMS",
            because: "el portador renovado tiene que seguir recordándolo, o la SIGUIENTE renovación volvería a perderlo");
    }

    [Fact]
    public async Task Un_Ingreso_Sin_Sistema_Sigue_Renovando_Sin_Acotar()
    {
        // La otra mitad, y no es adorno: acotar SIEMPRE por el sistema resuelto pondría verde la
        // prueba anterior y le escondería al portal multiproducto los perfiles de los demás
        // sistemas, que es justo lo que le permite ofrecer el cambio de sombrero.
        var ct = TestContext.Current.CancellationToken;
        var (cookie, _, ingreso) = await IngresarAsync(ct: ct);

        SistemaPedidoDelGrafo(ingreso).Should().BeNull();

        var respuesta = await ClienteConCookie(cookie).PostAsync("/api/v1/auth/refresh", content: null, ct);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        SistemaPedidoDelGrafo(cuerpo.RootElement).Should().BeNull(
            because: "sin petición de sistema en el ingreso, la renovación no debe inventar una");
    }

    // ── Andamiaje ───────────────────────────────────────────────────────────────

    private HttpClient NuevoCliente() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        // Sin contenedor de cookies: el logout borra la del contenedor, y lo que hay que comprobar
        // es lo contrario — que una COPIA conservada tampoco sirva. Se manejan a mano.
        HandleCookies = false,
    });

    private HttpClient ClienteConCookie(string cookie)
    {
        var cliente = NuevoCliente();
        cliente.DefaultRequestHeaders.Add("Cookie", cookie);
        return cliente;
    }

    private HttpClient ClienteConPortador(string portador)
    {
        var cliente = NuevoCliente();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", portador);
        return cliente;
    }

    /// <summary>Ingresa y devuelve la cookie, el portador y el cuerpo del ingreso.</summary>
    private async Task<(string Cookie, string Portador, JsonElement Cuerpo)> IngresarAsync(
        string? sistema = null, CancellationToken ct = default)
    {
        var respuesta = await NuevoCliente().PostAsJsonAsync("/api/v1/auth/login", new
        {
            tenantCode = Inquilino,
            username   = Usuario,
            password   = CoreDevDataSeeder.EffectiveEvolithDevPassword,
            rememberMe = false,
            systemCode = sistema,
        }, ct);

        var texto = await respuesta.Content.ReadAsStringAsync(ct);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, because: texto);

        var cookie = respuesta.Headers.TryGetValues("Set-Cookie", out var valores)
            ? valores.Select(v => v.Split(';')[0]).FirstOrDefault(v => v.StartsWith("ums.session", StringComparison.Ordinal))
            : null;
        cookie.Should().NotBeNullOrWhiteSpace(because: "el ingreso debe emitir la cookie de sesión");

        // El JsonDocument se descarta al salir; se clona el elemento para que siga siendo legible.
        using var documento = JsonDocument.Parse(texto);
        var cuerpo = documento.RootElement.Clone();

        return (cookie!, cuerpo.GetProperty("token").GetString()!, cuerpo);
    }

    private async Task<string> RefrescarAsync(string cookie, CancellationToken ct)
    {
        var respuesta = await ClienteConCookie(cookie).PostAsync("/api/v1/auth/refresh", content: null, ct);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await respuesta.Content.ReadAsStringAsync(ct));

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        return cuerpo.RootElement.GetProperty("token").GetString()!;
    }

    /// <summary>Lee un claim del portador SIN validar la firma: aquí interesa qué transporta.</summary>
    private static string? Claim(string portador, string tipo)
        => new JwtSecurityTokenHandler().ReadJwtToken(portador)
            .Claims.FirstOrDefault(c => c.Type == tipo)?.Value;

    private static string? SistemaPedidoDelGrafo(JsonElement cuerpo)
    {
        var contexto = cuerpo.GetProperty("authorizationGraph").GetProperty("context");
        return contexto.TryGetProperty("requestedSystem", out var pedido) && pedido.ValueKind == JsonValueKind.Object
            ? pedido.GetProperty("code").GetString()
            : null;
    }
}
