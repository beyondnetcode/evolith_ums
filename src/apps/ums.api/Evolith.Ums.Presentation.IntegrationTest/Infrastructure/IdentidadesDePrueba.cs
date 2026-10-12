using System.Net.Http.Json;
using System.Text.Json;
using Evolith.Ums.Infrastructure.Persistence.Seeders;

namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

/// <summary>
/// Provisiona identidades que EXISTEN y tienen permisos, para las pruebas que necesitan varios
/// actores distintos ([[G-280]], [[G-286]]).
///
/// <para>El arnés venía fabricando actores con GUIDs inventados —`a0000000-…`, `b0000000-…`—. Con el
/// atajo de Admin Root encendido daba igual: la claim `is_internal_admin` bastaba para atravesar los
/// manejadores guardados. Con el atajo apagado esos usuarios no existen, no tienen grafo y no tienen
/// un solo scope, así que la petición muere en el guardia y la prueba falla por su andamiaje y no
/// por lo que mide.</para>
///
/// <para>Se provisiona por la API REST y no tocando repositorios: es el mismo camino que recorre un
/// operador real, así que si ese camino se rompe, estas pruebas lo notan.</para>
/// </summary>
public static class IdentidadesDePrueba
{
    /// <summary>
    /// Crea una cuenta ACTIVA en EVOLITH con perfil de «Administrador de UMS» y devuelve su id.
    ///
    /// <para>Cada llamada devuelve una identidad DISTINTA, que es lo que exige la segregación de
    /// funciones: quien solicita no puede ser quien aprueba.</para>
    /// </summary>
    public static async Task<Guid> CrearAdministradorAsync(HttpClient clienteAdministrador, CancellationToken ct)
    {
        var tenantId = Guid.Parse(CoreDevDataSeeder.EvolithTenantId);

        var cuenta = await EnviarAsync(clienteAdministrador, HttpMethod.Post, "/api/v1/user-accounts", new
        {
            tenantId,
            branchId = (Guid?)null,
            email = $"actor.{Guid.NewGuid():N}@evolith.example",
            category = "Internal",
            identityReference = (string?)null,
            identityReferenceType = (string?)null,
        }, ct);
        cuenta.EnsureSuccessStatusCode();
        var userId = (await cuenta.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("userAccountId").GetGuid();

        (await EnviarAsync(clienteAdministrador, HttpMethod.Post, $"/api/v1/user-accounts/{userId}/activate", null, ct))
            .EnsureSuccessStatusCode();

        var (roleId, templateId) = await PlantillaDeAdministradorAsync(clienteAdministrador, ct);

        var perfil = await EnviarAsync(clienteAdministrador, HttpMethod.Post, "/api/v1/profiles",
            new { tenantId, userId, roleId, branchId = (Guid?)null }, ct);
        perfil.EnsureSuccessStatusCode();
        var profileId = (await perfil.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("profileId").GetGuid();

        (await EnviarAsync(clienteAdministrador, HttpMethod.Post, $"/api/v1/profiles/{profileId}/templates/{templateId}", null, ct))
            .EnsureSuccessStatusCode();

        return userId;
    }

    /// <summary>
    /// Emite la petición SIEMPRE contra EVOLITH, sea cual sea el inquilino del cliente que la llama.
    ///
    /// <para>La cuenta, su perfil y la plantilla de administrador viven en EVOLITH. Una prueba que
    /// opera sobre otro inquilino —el caso de la promoción de roles, que actúa sobre RANSA— lleva su
    /// cabecera apuntando allí, y con ella la lista de plantillas vuelve vacía: las de RANSA no
    /// incluyen «Administrador de UMS». El aprovisionamiento se hace donde vive la identidad.</para>
    /// </summary>
    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient cliente, HttpMethod metodo, string ruta, object? cuerpo, CancellationToken ct)
    {
        using var peticion = new HttpRequestMessage(metodo, ruta);
        foreach (var cabecera in cliente.DefaultRequestHeaders)
        {
            if (string.Equals(cabecera.Key, "X-Tenant-Id", StringComparison.OrdinalIgnoreCase)) continue;
            peticion.Headers.TryAddWithoutValidation(cabecera.Key, cabecera.Value);
        }
        peticion.Headers.TryAddWithoutValidation("X-Tenant-Id", CoreDevDataSeeder.EvolithTenantId);
        peticion.Headers.TryAddWithoutValidation("X-Is-Internal-Admin", "true");
        if (cuerpo is not null) peticion.Content = JsonContent.Create(cuerpo);
        return await cliente.SendAsync(peticion, ct);
    }

    /// <summary>
    /// La plantilla PUBLICADA de «Administrador de UMS», con su rol.
    ///
    /// <para>La más nueva de las publicadas, no la primera que aparezca: una plantilla publicada no
    /// admite ítems nuevos, así que ampliar el catálogo crea otra versión y quedarse con la vieja
    /// concede menos permisos de los que el catálogo declara.</para>
    /// </summary>
    private static async Task<(Guid RoleId, Guid TemplateId)> PlantillaDeAdministradorAsync(
        HttpClient cliente, CancellationToken ct)
    {
        var respuesta = await EnviarAsync(cliente, HttpMethod.Get, "/api/v1/permission-templates?page=1&pageSize=200", null, ct);
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct);

        var publicadas = cuerpo.GetProperty("items").EnumerateArray()
            .Where(t => t.TryGetProperty("status", out var estado) && estado.GetString() == "Published")
            .Where(t => t.TryGetProperty("roleName", out var rol) && rol.GetString() == "Administrador de UMS")
            .OrderBy(t => t.TryGetProperty("version", out var v) ? v.GetString() : string.Empty, StringComparer.Ordinal)
            .ToList();

        if (publicadas.Count == 0)
        {
            throw new InvalidOperationException(
                "la siembra debe dejar publicada la plantilla de «Administrador de UMS»; sin ella no hay a quien dar permisos");
        }

        var elegida = publicadas[^1];
        return (elegida.GetProperty("roleId").GetGuid(), elegida.GetProperty("templateId").GetGuid());
    }
}
