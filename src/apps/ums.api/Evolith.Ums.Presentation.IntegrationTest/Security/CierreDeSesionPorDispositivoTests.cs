using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Security;

/// <summary>
/// G-247 — cerrar sesión cierra ESTE dispositivo, y solo este.
///
/// <para>Antes no cerraba nada: <c>POST /auth/logout</c> respondía 200 y la misma cookie seguía
/// devolviendo 200 en <c>/auth/session</c>. Se midió en vivo contra un despliegue de dos réplicas y
/// también contra una sola, así que no era un problema de reparto de carga: <c>SignOutAsync</c>
/// solo le pide al navegador que borre la cookie, y el portador seguía siendo válido hasta
/// caducar.</para>
///
/// <para>La decisión de producto fue cerrar solo el dispositivo actual, no la cuenta entera. Estas
/// pruebas fijan las dos mitades de esa decisión —la que cierra y la que NO debe cerrar—, porque
/// una sola de ellas se puede satisfacer por accidente: revocar por usuario cerraría la sesión
/// actual igual de bien, y se llevaría por delante las demás.</para>
///
/// <para>Las cookies se manejan a mano, no con un contenedor: el logout borra la del contenedor, y
/// lo que hay que comprobar es justo lo contrario —que una COPIA conservada del portador tampoco
/// sirva—.</para>
/// </summary>
public sealed class CierreDeSesionPorDispositivoTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly UmsApiWebApplicationFactory _factory;

    public CierreDeSesionPorDispositivoTests(UmsApiWebApplicationFactory factory) => _factory = factory;

    private HttpClient NuevoCliente() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    /// <summary>Inicia sesión y devuelve la cookie emitida: eso es «un dispositivo».</summary>
    private async Task<string> AbrirSesionAsync(CancellationToken ct)
    {
        var cliente = NuevoCliente();
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new
        {
            tenantCode = "EVOLITH",
            username = "admin@evolith.example",
            password = CoreDevDataSeeder.EffectiveEvolithDevPassword,
            rememberMe = false,
        }, ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await respuesta.Content.ReadAsStringAsync(ct));

        var cookie = respuesta.Headers.TryGetValues("Set-Cookie", out var valores)
            ? valores.Select(v => v.Split(';')[0]).FirstOrDefault(v => v.StartsWith("ums.session", StringComparison.Ordinal))
            : null;

        cookie.Should().NotBeNullOrWhiteSpace(because: "el login debe emitir la cookie de sesión");
        return cookie!;
    }

    private HttpClient ClienteCon(string cookie)
    {
        var cliente = NuevoCliente();
        cliente.DefaultRequestHeaders.Add("Cookie", cookie);
        return cliente;
    }

    [Fact]
    public async Task Cerrar_Sesion_Invalida_Una_Copia_Del_Portador()
    {
        var ct = TestContext.Current.CancellationToken;
        var cookie = await AbrirSesionAsync(ct);

        (await ClienteCon(cookie).GetAsync("/api/v1/auth/session", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK, because: "la sesión recién abierta debe servir");

        (await ClienteCon(cookie).PostAsync("/api/v1/auth/logout", content: null, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await ClienteCon(cookie).GetAsync("/api/v1/auth/session", ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                because: "una copia del portador de una sesión cerrada no debe seguir abriendo la puerta");
    }

    [Fact]
    public async Task Cerrar_Sesion_En_Un_Dispositivo_No_Cierra_El_Otro()
    {
        var ct = TestContext.Current.CancellationToken;

        // Mismo usuario, dos sesiones: el portátil y el móvil.
        var portatil = await AbrirSesionAsync(ct);
        var movil = await AbrirSesionAsync(ct);
        portatil.Should().NotBe(movil, because: "cada login abre una sesión distinta");

        (await ClienteCon(portatil).PostAsync("/api/v1/auth/logout", content: null, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // ESTA es la mitad que se rompe si alguien «simplifica» revocando por usuario.
        (await ClienteCon(movil).GetAsync("/api/v1/auth/session", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK,
                because: "cerrar sesión en el portátil no debe echar al mismo usuario de su móvil (G-247)");

        // Y el portátil sigue cerrado: la comprobación anterior no vale si esta no se cumple.
        (await ClienteCon(portatil).GetAsync("/api/v1/auth/session", ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
