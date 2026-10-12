using Microsoft.Extensions.Caching.Distributed;

namespace Evolith.Ums.Infrastructure.Services;

/// <summary>
/// G-247 — sesiones cerradas, sobre <c>IDistributedCache</c>.
///
/// <para>Una sola implementación, y no un par Redis/InMemory como en
/// <see cref="ITokenRevocationStore"/>: <c>IDistributedCache</c> ya <b>es</b> Redis cuando hay
/// Redis configurado y memoria del proceso cuando no lo hay, así que duplicar la clase solo
/// duplicaría el sitio donde equivocarse. Lo que cambia entre los dos modos no es el código, es el
/// alcance, y el arranque ya lo declara en el log.</para>
///
/// <para>Clave: <c>sesion:cerrada:{sid}</c>. El valor guarda el instante de cierre para que quien
/// depure vea CUÁNDO se cerró, no solo que está cerrada. El TTL lo pone el propio almacén, así que
/// no hace falta ninguna tarea de limpieza: la entrada desaparece sola cuando el portador que
/// bloqueaba ya no valdría de todos modos.</para>
/// </summary>
public sealed class SessionRevocationStore(IDistributedCache cache) : ISessionRevocationStore
{
    private static string Clave(string sessionId) => $"sesion:cerrada:{sessionId}";

    /// <inheritdoc />
    public async Task RevocarAsync(string sessionId, DateTime cerrarHastaUtc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;

        // Ya pasó: el portador no puede seguir siendo válido, así que recordarlo no aporta nada.
        if (cerrarHastaUtc <= DateTime.UtcNow) return;

        await cache.SetStringAsync(
            Clave(sessionId),
            DateTime.UtcNow.ToString("O"),
            new DistributedCacheEntryOptions { AbsoluteExpiration = cerrarHastaUtc },
            ct);
    }

    /// <inheritdoc />
    public async Task<bool> EstaRevocadaAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return false;

        return await cache.GetStringAsync(Clave(sessionId), ct) is not null;
    }
}
