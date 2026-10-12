namespace Evolith.Ums.Infrastructure.Configuration;

/// <summary>
/// La cadena de conexión a Redis, en la forma que entiende StackExchange.Redis.
///
/// POR QUÉ EXISTE. El chart de Helm declara `REDIS_CONNECTION` como URI —`redis://ums-redis:6379`,
/// que es la convención de casi todo el mundo— y ese valor se pasaba TAL CUAL a
/// `ConnectionMultiplexer.Connect`, que no entiende el esquema: interpreta `redis` como host y
/// acaba componiendo `redis://ums-redis:6379:6379`, con el puerto duplicado. El síntoma es una
/// `RedisConnectionException` que acusa a Redis —vivo y sano— en vez de a la notación del valor.
///
/// Medido el 2026-08-02 al desplegar una imagen actual en el clúster: el proceso NO arrancaba. El
/// defecto llevaba latente desde que se escribió el camino de código, porque el clúster corría una
/// imagen anterior y nunca se ejecutaba.
///
/// VIVE AQUÍ, y no junto a uno de sus consumidores, porque son DOS: el registro de caché e
/// invalidación (`DependencyInjection`) y la protección de datos (`AuthenticationExtensions`).
/// Normalizar en uno solo es lo que ya pasó: el arranque siguió fallando desde el otro, con el
/// mismo mensaje y treinta minutos de diagnóstico por delante.
/// </summary>
public static class CadenaDeRedis
{
    /// <summary>
    /// Quita el esquema de una URI `redis://` o `rediss://` y deja intacto lo demás — puerto y
    /// cualquier cadena de opciones que venga detrás—, porque recortar de más cambiaría la
    /// conexión en vez de solo su notación. Un valor ya en forma `host:puerto` se devuelve igual.
    /// </summary>
    public static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return valor;

        var limpio = valor.Trim();
        foreach (var esquema in new[] { "redis://", "rediss://" })
        {
            if (limpio.StartsWith(esquema, StringComparison.OrdinalIgnoreCase))
            {
                return limpio[esquema.Length..];
            }
        }

        return limpio;
    }
}
