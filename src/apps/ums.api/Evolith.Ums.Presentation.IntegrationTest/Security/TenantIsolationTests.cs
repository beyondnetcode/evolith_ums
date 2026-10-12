using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using FluentAssertions;

namespace Evolith.Ums.Presentation.IntegrationTest.Security;

public sealed class TenantIsolationTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TenantIsolationTests(UmsApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task ZeroDataLeakage_QueryingOtherTenantUsers_ShouldBeRejected()
    {
        // 1. Iniciar sesión como administrador de RANSA_PERU
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            tenantCode = "RANSA_PERU",
            username = "gerente.operaciones@ransa.pe",
            password = CoreDevDataSeeder.SuperAdminPassword,
            rememberMe = false,
        }, TestContext.Current.CancellationToken);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));


        if (loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            var cookie = setCookies.FirstOrDefault(c => c.StartsWith("ums.session="));
            if (cookie != null)
            {
                _client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
            }
        }

        // Disable DevAuthMiddleware to ensure real cookie auth is processed correctly
        _client.DefaultRequestHeaders.Add("X-Disable-Dev-Auth", "true");

        // 2. Obtener el ID del Tenant NEPTUNIA
        // (En un ataque real, el atacante conoce o adivina el ID o el namespace)
        var neptuniaTenantId = "c9b736b4-6a84-48f8-b34d-176bc5a6d542"; // Seeded ID for Neptunia

        // 3. Intentar consultar usuarios de NEPTUNIA usando la API REST con el Token de RANSA
        // La API REST /api/v1/user-accounts debería estar filtrada automáticamente por el TenantContext.
        var restResponse = await _client.GetAsync($"/api/v1/user-accounts?tenantId={neptuniaTenantId}&page=1&pageSize=100", TestContext.Current.CancellationToken);

        // El request será 200 OK pero con 0 resultados de Neptunia (filtrado transparente) o 403 Forbidden.
        if (restResponse.StatusCode == HttpStatusCode.OK)
        {
            var restPayload = await restResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(restPayload);
            var items = document.RootElement.GetProperty("items");

            // Debemos verificar que la colección esté vacía (0 resultados devueltos)
            items.GetArrayLength().Should().Be(0, $"Tenant Isolation (Row-Level Security / Application Filter) fue violado. Response: {restPayload}");

            // Just to be absolutely sure, if it ever iterates, it shouldn't be Neptunia
            foreach (var item in items.EnumerateArray())
            {
                var returnedTenantId = item.GetProperty("tenantId").GetString();
                returnedTenantId.Should().NotBe(neptuniaTenantId, "Tenant Isolation Leak!");
            }
        }
        else
        {
            restResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
        }
    }
}
