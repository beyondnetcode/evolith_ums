using StackExchange.Redis;

namespace Evolith.Ums.Infrastructure.Services;

/// <summary>
/// G-248 — el cupo se cuenta en Redis, así que lo comparten todas las réplicas.
///
/// <para>El contador es un <c>INCR</c> sobre una clave que incluye el número de ventana, de modo
/// que cambiar de ventana es empezar a escribir en otra clave: no hace falta reiniciar nada ni
/// borrar nada, y dos réplicas que sirvan la misma petición coinciden en qué ventana es sin
/// hablarse. La clave anterior caduca sola.</para>
///
/// <para><c>INCR</c> primero y <c>EXPIRE</c> después, solo cuando el contador vale 1: es la
/// secuencia estándar, y su único riesgo —que el proceso muera entre las dos y deje una clave
/// eterna— está acotado porque la clave lleva el número de ventana y nunca se vuelve a usar. Aun
/// así se pide el vencimiento en el mismo viaje (batch), que reduce la ventana a prácticamente
/// nada.</para>
///
/// <para>Si Redis no responde, la petición se PERMITE. Es una decisión deliberada: un limitador es
/// una protección contra abuso, no un control de acceso, y convertir una caída de Redis en una
/// caída de UMS cambiaría un problema de capacidad por uno de disponibilidad. Queda registrado en
/// el log para que la degradación no sea invisible.</para>
/// </summary>
public sealed class LimitadorDePeticionesDistribuido(
    IConnectionMultiplexer redis,
    ILogger<LimitadorDePeticionesDistribuido> logger) : ILimitadorDePeticiones
{
    public async Task<ResultadoDelLimite> RegistrarAsync(
        string clave, int cupo, TimeSpan ventana, CancellationToken ct = default)
    {
        var ahora = DateTimeOffset.UtcNow;
        var ticksDeVentana = Math.Max(1, ventana.Ticks);
        var numeroDeVentana = ahora.UtcTicks / ticksDeVentana;
        var restante = TimeSpan.FromTicks(((numeroDeVentana + 1) * ticksDeVentana) - ahora.UtcTicks);

        RedisKey redisKey = $"limite:{clave}:{numeroDeVentana}";

        try
        {
            var db = redis.GetDatabase();

            // Un solo viaje: incrementar y, si es la primera de la ventana, fijarle el vencimiento.
            var batch = db.CreateBatch();
            var incremento = batch.StringIncrementAsync(redisKey);
            // El margen extra evita que la clave caduque justo antes de que termine su ventana por
            // una diferencia de reloj entre el proceso y Redis.
            var vencimiento = batch.KeyExpireAsync(redisKey, ventana + TimeSpan.FromSeconds(5), ExpireWhen.HasNoExpiry);
            batch.Execute();

            var consumidas = await incremento.ConfigureAwait(false);
            await vencimiento.ConfigureAwait(false);

            return new ResultadoDelLimite(consumidas <= cupo, consumidas, restante);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "El límite de peticiones no pudo consultarse en Redis; la petición se permite. " +
                "El cupo queda sin aplicar mientras dure la incidencia (G-248).");

            return new ResultadoDelLimite(Permitida: true, Consumidas: 0, EsperaSugerida: restante);
        }
    }
}
