using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization.Graph;

namespace Evolith.Ums.Infrastructure.Authorization.Graph;

/// <summary>
/// Retención del grafo sobre <see cref="IDistributedCache"/>.
///
/// UNA SOLA IMPLEMENTACIÓN, y no dos. `IDistributedCache` ya resuelve a memoria del proceso por
/// omisión y a Redis en cuanto `Redis:Connection` o `REDIS_CONNECTION` están definidas: ese
/// interruptor vive en `DependencyInjection` y gobierna desde G-169 la revocación de tokens, la
/// caché de configuración y el limitador de peticiones. Duplicarlo aquí daría dos verdades sobre
/// dónde vive el estado compartido, y la que fallara lo haría en silencio.
///
/// EL MODO DEGRADADO SE DECLARA, no se deduce. Con una sola réplica, memoria y Redis se comportan
/// igual; con varias, un grafo retenido en el proceso hace que la misma sesión vea permisos
/// distintos según el pod que la atienda. El arranque ya declara qué quedó activo
/// (`DeclararEstadoDistribuido`), y esta clase no vuelve a decidirlo.
/// </summary>
public sealed class CacheDeGrafoDistribuida : ICacheDeGrafo
{
    /// <summary>Vida del grafo retenido, en minutos. Por omisión 15.</summary>
    public const string ClaveVida = "Authorization:GraphCacheMinutes";

    private const string PrefijoGrafo = "ums:graph:";
    private const string PrefijoVersion = "ums:graphver:";
    private const string PrefijoVersionInquilino = "ums:graphver:tenant:";
    private const string ClaveVersionGlobal = "ums:graphver:global";
    private const int VidaPorOmisionMinutos = 15;

    /// <summary>
    /// La versión de un perfil vive MÁS que cualquier grafo suyo. Si caducara antes, volvería a
    /// empezar en cero y una clave vieja —todavía viva— pasaría a ser alcanzable otra vez: un
    /// permiso revocado resucitado por un detalle de caducidad.
    /// </summary>
    private static readonly TimeSpan VidaDeLaVersion = TimeSpan.FromDays(30);

    /// <summary>
    /// Las opciones con las que una API mínima emite un objeto: <see cref="JsonSerializerDefaults.Web"/>.
    /// Se fijan aquí porque lo retenido se devuelve SIN volver a serializar, así que estos bytes son
    /// literalmente la respuesta. Si el host llegara a configurar las suyas, este es el sitio que
    /// hay que igualar — y `CacheDeGrafoSirveLaFormaDelCable` es la prueba que lo detecta.
    /// </summary>
    private static readonly JsonSerializerOptions FormaDeCable = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<CacheDeGrafoDistribuida>? _logger;

    public CacheDeGrafoDistribuida(
        IDistributedCache cache,
        IConfiguration? configuration = null,
        ILogger<CacheDeGrafoDistribuida>? logger = null)
    {
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    private TimeSpan Vida =>
        TimeSpan.FromMinutes(
            int.TryParse(_configuration?[ClaveVida], out var m) && m > 0 ? m : VidaPorOmisionMinutos);

    public async Task<string> ClaveAsync(
        Guid? tenantId, Guid profileId, string? systemCode, string? proyeccion, CancellationToken cancellationToken = default)
    {
        // Las TRES versiones entran en la clave, y basta que UNA avance para que todo lo retenido
        // bajo ella quede inalcanzable. Es lo que permite invalidar al nivel que corresponda al
        // cambio —un perfil, un inquilino, o todo— sin enumerar claves, que `IDistributedCache` no
        // sabe hacer. Se leen en paralelo: son tres viajes al mismo almacén y encadenarlos
        // triplicaría la latencia de cada acierto.
        var lecturas = await Task.WhenAll(
            VersionAsync(ClaveVersionGlobal, cancellationToken),
            tenantId is null
                ? Task.FromResult(0L)
                : VersionAsync(PrefijoVersionInquilino + tenantId.Value.ToString("N"), cancellationToken),
            VersionAsync(PrefijoVersion + profileId.ToString("N"), cancellationToken));

        var versiones = string.Join(
            '.', lecturas.Select(v => v.ToString(CultureInfo.InvariantCulture)));

        // El sistema y la proyección se resumen en un hash corto: entran en la clave porque
        // producen grafos DISTINTOS —`?include=` recorta secciones enteras— y servir uno por otro
        // daría un grafo sin `domainPermissions` que pasaría por completo, concediendo en silencio
        // lo que UMS había denegado.
        var variante = Resumen($"{systemCode ?? "-"}|{proyeccion ?? "-"}");
        return $"{PrefijoGrafo}{profileId:N}:{versiones}:{variante}";
    }

    public async Task<string?> ObtenerAsync(string claveDeGrafo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(claveDeGrafo)) return null;

        var bytes = await _cache.GetAsync(claveDeGrafo, cancellationToken);
        if (bytes is null || bytes.Length == 0) return null;

        var json = Encoding.UTF8.GetString(bytes);
        try
        {
            // Se comprueba que sea JSON legible antes de devolverlo. Entregar una cadena rota
            // trasladaría el fallo al consumidor, donde ya no se sabe que vino de la caché.
            using var _ = JsonDocument.Parse(json);
            return json;
        }
        catch (JsonException ex)
        {
            // FALLA CERRADO: un grafo ilegible es indistinguible de uno que no está, y devolver
            // null obliga a reconstruirlo. Devolverlo a medias concedería por ausencia de
            // denegaciones. Ocurre al cambiar la forma del grafo con entradas vivas de la versión
            // anterior — un despliegue, sin más.
            _logger?.LogWarning(ex,
                "Grafo retenido ilegible en {Clave}; se descarta y se reconstruirá. "
                + "Suele ser una entrada de una versión anterior del grafo tras un despliegue.",
                claveDeGrafo);
            await _cache.RemoveAsync(claveDeGrafo, cancellationToken);
            return null;
        }
    }

    public async Task GuardarAsync(
        string claveDeGrafo, AuthorizationGraph grafo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(claveDeGrafo) || grafo is null) return;

        // El MISMO mapeador Y LAS MISMAS OPCIONES que emite el grafo por cable (D-025). El mapeador
        // solo no basta: `AuthGraphPayload` no lleva ni un `[JsonPropertyName]`, así que su forma la
        // decide entera el serializador. Serializar sin opciones daba `Context` en PascalCase
        // mientras el endpoint emite `context` en camelCase --las de `JsonSerializerDefaults.Web`,
        // que es lo que usan las APIs mínimas--. Lo retenido se sirve TAL CUAL, sin volver a
        // serializar, de modo que una diferencia aquí llegaría al satélite como un grafo con otras
        // claves: no fallaría, simplemente no encontraría nada y todo quedaría denegado.
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(AuthGraphPayload.Build(grafo), FormaDeCable));

        await _cache.SetAsync(
            claveDeGrafo,
            bytes,
            // Vida ABSOLUTA, no deslizante. Con vida deslizante una sesión activa nunca releería
            // sus permisos y un cambio de rol tardaría en aplicarse lo que durase la jornada.
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Vida },
            cancellationToken);
    }

    public Task InvalidarPerfilAsync(Guid profileId, CancellationToken cancellationToken = default)
        => AvanzarAsync(PrefijoVersion + profileId.ToString("N"), "perfil " + profileId, cancellationToken);

    public Task InvalidarInquilinoAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => AvanzarAsync(PrefijoVersionInquilino + tenantId.ToString("N"), "inquilino " + tenantId, cancellationToken);

    public Task InvalidarGlobalAsync(CancellationToken cancellationToken = default)
        => AvanzarAsync(ClaveVersionGlobal, "TODOS los inquilinos", cancellationToken);

    /// <summary>
    /// Avanza un contador de versión. Es la única forma de invalidar aquí: ver
    /// <see cref="ICacheDeGrafo.InvalidarPerfilAsync"/> sobre por qué no se borra.
    ///
    /// <para>NO es atómico —leer y escribir son dos viajes— y no necesita serlo. Dos avances
    /// concurrentes pueden acabar en el mismo número; el efecto que importa es que el número
    /// CAMBIE respecto al que llevan las claves ya escritas, y eso ocurre igual. Un
    /// incremento atómico exigiría una operación que <c>IDistributedCache</c> no expone, y
    /// bajar a la API de Redis rompería el modo memoria que es el mínimo por defecto.</para>
    /// </summary>
    private async Task AvanzarAsync(string claveDeVersion, string queSeInvalida, CancellationToken cancellationToken)
    {
        var siguiente = await VersionAsync(claveDeVersion, cancellationToken) + 1;

        await _cache.SetAsync(
            claveDeVersion,
            Encoding.UTF8.GetBytes(siguiente.ToString(CultureInfo.InvariantCulture)),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = VidaDeLaVersion },
            cancellationToken);

        _logger?.LogInformation(
            "Grafo invalidado para {Alcance}: versión {Version}. Se reconstruirá en la próxima petición.",
            queSeInvalida, siguiente);
    }

    private async Task<long> VersionAsync(string claveDeVersion, CancellationToken cancellationToken)
    {
        var bytes = await _cache.GetAsync(claveDeVersion, cancellationToken);
        if (bytes is null || bytes.Length == 0) return 0;
        return long.TryParse(Encoding.UTF8.GetString(bytes), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static string Resumen(string texto)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant()[..12];
}
