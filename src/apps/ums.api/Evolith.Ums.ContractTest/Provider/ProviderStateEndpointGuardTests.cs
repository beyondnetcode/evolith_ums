using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Evolith.Ums.ContractTest.Provider;

/// <summary>
/// G-101: verifica la guarda de entorno de `POST /_pact/provider-states`.
///
/// El endpoint siembra datos de dominio arbitrarios de forma anónima, por lo que
/// sólo debe registrarse en entornos de desarrollo/contract-test. Estas pruebas
/// confirman el contraste:
///   - En «Development» (factoría base) el endpoint responde 200 OK.
///   - En «Production» el endpoint NO está registrado → 404 Not Found.
///
/// Comparte la colección «Provider» con <see cref="UmsApiProviderTests"/> para
/// serializar el arranque de hosts: con hosting mínimo (WebApplication) la
/// resolución de entorno de WebApplicationFactory se contamina entre hosts que se
/// construyen en paralelo, de modo que un host «Production» concurrente haría que
/// el proveedor viese el endpoint como no registrado (404). En secuencia, cada host
/// resuelve su entorno correctamente.
/// </summary>
[Collection("Provider")]
public sealed class ProviderStateEndpointGuardTests
{
    private static readonly object ProviderStateBody = new { state = "at least one tenant exists" };

    [Fact]
    [Trait("pact", "guard")]
    public async Task ProviderStates_InProduction_IsNotRegistered()
    {
        using var factory = new ProductionContractTestWebApplicationFactory();

        // Base https para no toparse con UseHttpsRedirection (activo fuera de desarrollo):
        // así la petición llega al enrutado, que devuelve 404 al no existir el endpoint.
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress       = new Uri("https://localhost"),
        });

        using var response = await client.PostAsJsonAsync(
            "/_pact/provider-states", ProviderStateBody, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("pact", "guard")]
    public async Task ProviderStates_InDevelopment_IsRegistered()
    {
        using var factory = new ContractTestWebApplicationFactory();

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.PostAsJsonAsync(
            "/_pact/provider-states", ProviderStateBody, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
