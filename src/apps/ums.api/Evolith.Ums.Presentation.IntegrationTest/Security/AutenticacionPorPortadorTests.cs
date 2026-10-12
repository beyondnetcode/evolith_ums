using System.Diagnostics.Metrics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Evolith.Ums.Application.Authorization.Graph;
using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace Evolith.Ums.Presentation.IntegrationTest.Security;

/// <summary>
/// G-191 — UMS valida sus PROPIOS tokens y la superficie de satélites autentica por portador.
///
/// Antes, `GET /api/v1/client/graph` con un `Authorization: Bearer` legítimo respondía 302: en
/// desarrollo no existía manejador de portador y el esquema por defecto era la cookie, así que el
/// reto redirigía al formulario de acceso. Estas pruebas fijan el contrato que consume cualquier
/// sistema satélite:
///
///   portador válido  → 200
///   sin portador     → 401 (nunca 3xx)
///   portador inválido, caducado, de otra firma o de otro emisor → 401
///   portador HS256, aunque la clave sea la pública de UMS → 401 (G-199)
///   cookie `ums.session` → sigue autenticando al portal, y NO sirve para la superficie de satélite
/// </summary>
public sealed class AutenticacionPorPortadorTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private const string EmisorDePrueba = "ums-api";

    /// <summary>
    /// Una clave RSA que NO es la de UMS. Prueba que la firma se valida contra la clave
    /// pública del host y no contra cualquier RSA bien formada.
    /// </summary>
    private static readonly SigningCredentials CredencialesAjenas = new(
        new RsaSecurityKey(System.Security.Cryptography.RSA.Create(2048)),
        SecurityAlgorithms.RsaSha256);
    private const string AudienciaDePrueba = "ums-web-app";

    private const string InquilinoCliente = "COMEX_ANDINA";
    private const string UsuarioCliente = "usuario.impo@comexandina.com.pe";

    private readonly UmsApiWebApplicationFactory _factory;

    public AutenticacionPorPortadorTests(UmsApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ClientGraph_ConPortadorValido_Devuelve200YElGrafoVigente()
    {
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();
        var token = await ObtenerPortadorAsync(cliente, ct);

        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var respuesta = await cliente.GetAsync("/api/v1/client/graph", ct);

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"un satélite con portador válido debe recibir su grafo, no una redirección (G-191). Cuerpo: {cuerpo}");

        using var payload = JsonDocument.Parse(cuerpo);
        payload.RootElement.TryGetProperty("context", out _).Should().BeTrue(
            "el grafo se reconstruye en el momento y trae su contexto de usuario e inquilino");
    }

    [Fact]
    public async Task ClientGraph_DosVecesSeguidas_DevuelveExactamenteElMismoCuerpo()
    {
        // La segunda llamada se sirve de lo retenido, que se devuelve TAL CUAL sin volver a
        // serializar. Si lo guardado no tuviera la forma exacta del cable, el satélite recibiría un
        // grafo con otras claves en la segunda petición y no en la primera: no un error, sino todo
        // denegado a partir de la segunda vez. Comparar los cuerpos ENTEROS es lo que lo detecta;
        // comprobar el código de estado no vería nada.
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();
        var token = await ObtenerPortadorAsync(cliente, ct);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var primera = await cliente.GetAsync("/api/v1/client/graph", ct);
        var segunda = await cliente.GetAsync("/api/v1/client/graph", ct);

        primera.StatusCode.Should().Be(HttpStatusCode.OK);
        segunda.StatusCode.Should().Be(HttpStatusCode.OK);

        var cuerpoPrimera = await primera.Content.ReadAsStringAsync(ct);
        var cuerpoSegunda = await segunda.Content.ReadAsStringAsync(ct);

        cuerpoSegunda.Should().Be(cuerpoPrimera,
            "lo retenido se sirve sin reserializar: su forma debe ser IDÉNTICA a la que construye el endpoint");

        segunda.Content.Headers.ContentType?.ToString()
            .Should().Be(primera.Content.Headers.ContentType?.ToString(),
                "un acierto y un fallo de caché no pueden distinguirse por la cabecera");
    }

    [Fact]
    public async Task ClientGraph_DosVecesSeguidas_ConstruyeElGrafoUnaSolaVez()
    {
        // Que los dos cuerpos coincidan NO prueba que la caché acierte: también coincidirían
        // reconstruyendo dos veces. Lo que lo prueba es contar construcciones, y para eso ya existe
        // instrumentación: `GraphMetrics.Duracion` registra UNA medida por grafo construido.
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();
        var token = await ObtenerPortadorAsync(cliente, ct);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var construcciones = 0;

        using var escucha = new MeterListener();
        escucha.InstrumentPublished = (instrumento, oyente) =>
        {
            if (instrumento.Meter.Name == GraphMetrics.MeterName && instrumento.Name == GraphMetrics.Duracion.Name)
            {
                oyente.EnableMeasurementEvents(instrumento);
            }
        };
        escucha.SetMeasurementEventCallback<double>((_, _, _, _) => Interlocked.Increment(ref construcciones));
        escucha.Start();

        (await cliente.GetAsync("/api/v1/client/graph", ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await cliente.GetAsync("/api/v1/client/graph", ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        construcciones.Should().Be(1,
            "la segunda petición debe servirse de lo retenido, no reconstruir el grafo");
    }

    [Fact]
    public async Task ClientGraph_SinPortador_Devuelve401YNoRedirige()
    {
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();

        var respuesta = await cliente.GetAsync("/api/v1/client/graph", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "un cliente de API no sigue un `Location`: la ausencia de credencial es 401, nunca 302");
        respuesta.Headers.Location.Should().BeNull("ninguna ruta /api/** puede responder con redirección");
    }

    [Fact]
    public async Task ClientGraph_ConPortadorMalformado_Devuelve401()
    {
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", "no-es-un-jwt");

        var respuesta = await cliente.GetAsync("/api/v1/client/graph", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClientGraph_ConLaFirmaAlterada_Devuelve401()
    {
        // El token es legítimo salvo por la firma: prueba que la comprobación criptográfica
        // ocurre de verdad y no basta con presentar algo con forma de JWT.
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();
        var token = await ObtenerPortadorAsync(cliente, ct);

        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token + "xyz");
        var respuesta = await cliente.GetAsync("/api/v1/client/graph", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClientGraph_ConPortadorCaducado_Devuelve401()
    {
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", ForjarToken(
            CredencialesDelHost(), EmisorDePrueba, DateTime.UtcNow.AddHours(-1)));

        var respuesta = await cliente.GetAsync("/api/v1/client/graph", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "la vida del token se valida: un portador caducado no sirve aunque su firma sea correcta");
    }

    [Fact]
    public async Task ClientGraph_ConPortadorFirmadoConOtraClave_Devuelve401()
    {
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();

        cliente.DefaultRequestHeaders.Authorization = new("Bearer", ForjarToken(
            CredencialesAjenas, EmisorDePrueba, DateTime.UtcNow.AddHours(1)));

        var respuesta = await cliente.GetAsync("/api/v1/client/graph", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "UMS valida la firma contra SU clave pública: un token de otra RSA no entra");
    }

    [Fact]
    public async Task ClientGraph_ConPortadorHs256_Devuelve401AunqueLaClaveSeaLaPublicaDeUms()
    {
        // G-199 — la rama simétrica se eliminó al cerrar la etapa E3 de ADR-0157. Esta prueba es
        // el pestillo: si alguien reintroduce HS256 «por compatibilidad», vuelve a rojo.
        //
        // Se firma con la clave PÚBLICA de UMS usada como secreto HMAC, que es exactamente el
        // ataque de confusión de `alg` de RFC 8725 §2.1: material publicado —cualquiera lo
        // descarga de /.well-known/jwks.json— convertido en clave de firma. Lo único que lo
        // impide es la lista blanca de un solo algoritmo.
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();

        var material = _factory.Services
            .GetRequiredService<Evolith.Ums.Presentation.Services.MaterialDeFirma>();

        var publicaComoSecreto = JsonSerializer.Serialize(material.ComoJwkPublico());
        var credencialesHmac = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(publicaComoSecreto)),
            SecurityAlgorithms.HmacSha256);

        cliente.DefaultRequestHeaders.Authorization = new("Bearer", ForjarToken(
            credencialesHmac, EmisorDePrueba, DateTime.UtcNow.AddHours(1)));

        var respuesta = await cliente.GetAsync("/api/v1/client/graph", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "UMS ya no acepta HS256: la lista blanca solo admite RS256 (G-199)");
    }

    [Fact]
    public async Task ClientGraph_ConPortadorDeOtroEmisor_Devuelve401()
    {
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", ForjarToken(
            CredencialesDelHost(), "emisor-ajeno", DateTime.UtcNow.AddHours(1)));

        var respuesta = await cliente.GetAsync("/api/v1/client/graph", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "el emisor se valida: un token con `iss` distinto no es de esta instalación");
    }

    [Fact]
    public async Task ClientGraph_ConSoloLaCookieDelPortal_Devuelve401()
    {
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();
        var cookie = await IniciarSesionWebAsync(cliente, ct);

        cliente.DefaultRequestHeaders.Remove("Cookie");
        cliente.DefaultRequestHeaders.Add("Cookie", cookie);
        var respuesta = await cliente.GetAsync("/api/v1/client/graph", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "la política `Satelite` fija el esquema portador: la sesión de navegador no habilita la superficie servidor a servidor");
        respuesta.Headers.Location.Should().BeNull("tampoco con cookie puede redirigir una ruta /api/**");
    }

    [Fact]
    public async Task Session_ConPortador_Devuelve200YNoRedirige()
    {
        // G-187 (parte del 302): preguntar «¿esta sesión sigue viva?» con portador ya no redirige.
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();
        var token = await ObtenerPortadorAsync(cliente, ct);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var respuesta = await cliente.GetAsync("/api/v1/auth/session", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        payload.RootElement.GetProperty("tenantCode").GetString().Should().Be(InquilinoCliente);
    }

    [Fact]
    public async Task Session_SinCredencial_Devuelve401()
    {
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();

        var respuesta = await cliente.GetAsync("/api/v1/auth/session", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        respuesta.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task PortalWeb_LoginYSesionPorCookie_SiguenFuncionando()
    {
        // El portal no debe notar ningún cambio: la cookie `ums.session` sigue siendo su credencial.
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();
        var cookie = await IniciarSesionWebAsync(cliente, ct);

        cliente.DefaultRequestHeaders.Remove("Cookie");
        cliente.DefaultRequestHeaders.Add("Cookie", cookie);

        var sesion = await cliente.GetAsync("/api/v1/auth/session", ct);
        sesion.StatusCode.Should().Be(HttpStatusCode.OK,
            "la sesión de cookie del portal debe seguir resolviéndose por el esquema de cookie");

        using var payload = JsonDocument.Parse(await sesion.Content.ReadAsStringAsync(ct));
        payload.RootElement.GetProperty("tenantCode").GetString().Should().Be(InquilinoCliente);
        payload.RootElement.GetProperty("email").GetString().Should().Be(UsuarioCliente);
    }

    [Fact]
    public async Task EndpointDeNegocio_ConPortadorInvalido_NoCaeEnLasClaimsDeDesarrollo()
    {
        // El host de pruebas corre en «Development», donde DevAuthMiddleware inyecta identidad de
        // conveniencia. Si esa inyección tapara el rechazo del portador, un token falsificado
        // acabaría respondiendo 200: la exención por cabecera `Authorization` lo impide (G-191).
        var ct = TestContext.Current.CancellationToken;
        var cliente = CrearCliente();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", ForjarToken(
            CredencialesAjenas, EmisorDePrueba, DateTime.UtcNow.AddHours(1)));

        var respuesta = await cliente.GetAsync("/api/v1/user-accounts?page=1&pageSize=1", ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────────

    private HttpClient CrearCliente() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        // Sin seguir redirecciones: si algo respondiera 302, la prueba tiene que verlo.
        AllowAutoRedirect = false,
        HandleCookies = false,
    });

    private static async Task<string> ObtenerPortadorAsync(HttpClient cliente, CancellationToken ct)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/client/authenticate", new
        {
            tenantCode = InquilinoCliente,
            username = UsuarioCliente,
            password = CoreDevDataSeeder.EffectiveEvolithDevPassword,
        }, ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        var token = payload.RootElement.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();
        return token!;
    }

    private static async Task<string> IniciarSesionWebAsync(HttpClient cliente, CancellationToken ct)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new
        {
            tenantCode = InquilinoCliente,
            username = UsuarioCliente,
            password = CoreDevDataSeeder.EffectiveEvolithDevPassword,
            rememberMe = false,
        }, ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, "el login del portal debe seguir intacto");
        var setCookie = respuesta.Headers.GetValues("Set-Cookie").FirstOrDefault();
        setCookie.Should().NotBeNullOrEmpty("el portal se autentica con la cookie ums.session");
        setCookie.Should().Contain("ums.session");
        return setCookie!.Split(';')[0];
    }

    /// <summary>
    /// Las credenciales REALES del host bajo prueba. La factoría corre en Development, donde
    /// <c>MaterialDeFirma</c> genera un par RSA efímero: no hay clave fija que compartir con la
    /// prueba, así que se resuelve el mismo singleton que usa la API para firmar.
    /// </summary>
    private SigningCredentials CredencialesDelHost() => _factory.Services
        .GetRequiredService<Evolith.Ums.Presentation.Services.MaterialDeFirma>().Credenciales;

    private static string ForjarToken(SigningCredentials credenciales, string emisor, DateTime expiraEnUtc)
    {
        var token = new JwtSecurityToken(
            issuer: emisor,
            audience: AudienciaDePrueba,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, UsuarioCliente),
                new Claim(JwtRegisteredClaimNames.Email, UsuarioCliente),
                new Claim("tenant_code", InquilinoCliente),
            ],
            notBefore: expiraEnUtc.AddHours(-1),
            expires: expiraEnUtc,
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
