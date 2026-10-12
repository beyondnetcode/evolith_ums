using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Security;

/// <summary>
/// G-248 — el cupo de peticiones se cuenta por QUIEN llama, no por la IP de la que vienen todos.
///
/// <para>El limitador anterior corría antes de <c>UseAuthentication</c>, así que
/// <c>HttpContext.User</c> venía vacío y la clave de reparto caía siempre a la IP. Medido el
/// 2026-08-04 con el cupo en 6: tres peticiones de una cuenta y tres de otra bastaban para que
/// AMBAS recibieran 429. Detrás de un Ingress —donde la IP que ve el proceso es la del
/// controlador— eso significaba un único cubo para todo el tráfico.</para>
///
/// <para>Estas pruebas comprueban las dos mitades: que el cupo se aplica, y que NO se comparte
/// entre usuarios distintos. La primera sola pasaría también con el limitador roto.</para>
/// </summary>
public sealed class LimiteDePeticionesTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly UmsApiWebApplicationFactory _factory;

    public LimiteDePeticionesTests(UmsApiWebApplicationFactory factory) => _factory = factory;

    private HttpClient NuevoCliente() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    private async Task<string> AbrirSesionAsync(string tenantCode, string usuario, CancellationToken ct)
    {
        var respuesta = await NuevoCliente().PostAsJsonAsync("/api/v1/auth/login", new
        {
            tenantCode,
            username = usuario,
            password = CoreDevDataSeeder.EffectiveEvolithDevPassword,
            rememberMe = false,
        }, ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await respuesta.Content.ReadAsStringAsync(ct));

        var cookie = respuesta.Headers.TryGetValues("Set-Cookie", out var valores)
            ? valores.Select(v => v.Split(';')[0])
                     .FirstOrDefault(v => v.StartsWith("ums.session", StringComparison.Ordinal))
            : null;

        cookie.Should().NotBeNullOrWhiteSpace();
        return cookie!;
    }

    private HttpClient ClienteCon(string cookie)
    {
        var cliente = NuevoCliente();
        cliente.DefaultRequestHeaders.Add("Cookie", cookie);
        return cliente;
    }

    [Fact]
    public async Task Dos_Usuarios_Distintos_No_Comparten_El_Cupo()
    {
        var ct = TestContext.Current.CancellationToken;

        // Dos cuentas de inquilinos distintos, desde la MISMA dirección: es el caso que el
        // limitador anterior confundía en un solo cubo.
        var uno = await AbrirSesionAsync("EVOLITH", "admin@evolith.example", ct);
        var otro = await AbrirSesionAsync("COMEX_ANDINA", "usuario.impo@comexandina.com.pe", ct);

        // Se gastan bastantes peticiones con la PRIMERA cuenta. Con el cupo por defecto (100/min)
        // no se llega a agotar; lo que se comprueba es que lo que gaste una no se le descuenta a la
        // otra, que es la propiedad que estaba rota.
        for (var i = 0; i < 40; i++)
        {
            var r = await ClienteCon(uno).GetAsync("/api/v1/auth/session", ct);
            r.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                because: "40 peticiones no deben agotar un cupo de 100");
        }

        var deLaOtraCuenta = await ClienteCon(otro).GetAsync("/api/v1/auth/session", ct);
        deLaOtraCuenta.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "lo que gasta una cuenta no puede descontarse del cupo de otra (G-248)");
    }

    [Fact]
    public async Task El_Cupo_Se_Aplica_Y_Responde_429_Con_Retry_After()
    {
        var ct = TestContext.Current.CancellationToken;

        // Sin sesión: el cupo se cuenta por IP. Una cabecera `X-Forwarded-For` propia aísla esta
        // prueba de las demás — si compartiera cubo con ellas, el resultado dependería del orden
        // de ejecución, que es la clase de prueba que falla los martes.
        var cliente = NuevoCliente();
        cliente.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.77");

        HttpResponseMessage? rechazada = null;
        for (var i = 0; i < 130 && rechazada is null; i++)
        {
            var r = await cliente.GetAsync("/api/v1/auth/session", ct);
            if (r.StatusCode == HttpStatusCode.TooManyRequests) rechazada = r;
        }

        rechazada.Should().NotBeNull(because: "pasado el cupo de 100 por ventana debe llegar un 429");
        rechazada!.Headers.RetryAfter.Should().NotBeNull(
            because: "un 429 sin `Retry-After` obliga a quien llama a adivinar cuándo reintentar");

        var cuerpo = await rechazada.Content.ReadAsStringAsync(ct);
        cuerpo.Should().Contain("Too Many Requests");
    }
}
