using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity;

public sealed class AuthRestEndpointTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthRestEndpointTests(UmsApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task Login_WithSeededCommercialTenantCredentials_ShouldSucceed()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            tenantCode = "RANSA_PERU",
            username = "gerente.operaciones@ransa.pe",
            password = CoreDevDataSeeder.SuperAdminPassword,
            rememberMe = false,
        }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        payload.RootElement.GetProperty("tenantCode").GetString().Should().Be("RANSA_PERU");
        payload.RootElement.GetProperty("email").GetString().Should().Be("gerente.operaciones@ransa.pe");
        payload.RootElement.GetProperty("isInternalAdmin").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// G-184: el portal puede pedir SU sistema al iniciar sesion.
    ///
    /// <para>El sujeto es el administrador de EVOLITH justamente porque tiene perfil en DOS sistemas
    /// —`SIL` y `UMS`—: sin `systemCode`, el grafo lo resuelve un desempate y puede caer en
    /// cualquiera de los dos, que es como el portal acababa pintando los menus de un satelite. La
    /// asercion mira el claim `sys_suite` del token, que es donde viaja la suite del grafo emitido.</para>
    /// </summary>
    [Fact]
    public async Task Login_ConSystemCode_AcotaElGrafoAEseSistema()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            tenantCode = "EVOLITH",
            username = CoreDevDataSeeder.EffectiveEvolithRootAdminEmail,
            password = CoreDevDataSeeder.EffectiveEvolithDevPassword,
            rememberMe = false,
            systemCode = "UMS",
        }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var token = payload.RootElement.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();

        ClaimDelToken(token!, "sys_suite").Should().Be("UMS");
    }

    /// <summary>
    /// La otra mitad del contrato: sin `systemCode` NO cambia nada. El campo es opcional y el
    /// desempate de siempre sigue gobernando, que es lo que necesita un portal multiproducto —y lo
    /// que evita que este cambio rompa a quien ya llamaba a este endpoint.
    /// </summary>
    [Fact]
    public async Task Login_SinSystemCode_SigueResolviendoPorDesempate()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            tenantCode = "EVOLITH",
            username = CoreDevDataSeeder.EffectiveEvolithRootAdminEmail,
            password = CoreDevDataSeeder.EffectiveEvolithDevPassword,
            rememberMe = false,
        }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var token = payload.RootElement.GetProperty("token").GetString();
        ClaimDelToken(token!, "sys_suite").Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>Lee un claim del token sin verificar la firma: aqui importa QUE declara, no si es valido.</summary>
    private static string? ClaimDelToken(string token, string claim)
    {
        var partes = token.Split('.');
        var carga = partes[1].Replace('-', '+').Replace('_', '/');
        carga = carga.PadRight(carga.Length + ((4 - (carga.Length % 4)) % 4), '=');
        using var doc = JsonDocument.Parse(Convert.FromBase64String(carga));
        return doc.RootElement.TryGetProperty(claim, out var valor) ? valor.GetString() : null;
    }
}
