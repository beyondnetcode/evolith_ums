using System.Collections.Concurrent;

namespace Evolith.Ums.Infrastructure.Services;

/// <summary>
/// G-248 — respaldo cuando no hay Redis: cuenta dentro del proceso.
///
/// <para>Es el comportamiento anterior, y solo es correcto con UNA réplica. Existe para que el
/// desarrollo local y las pruebas no exijan un Redis, no como modo de despliegue: el arranque ya
/// declara en el log si el estado compartido quedó distribuido o en memoria, y esta clase cae del
/// lado «en memoria».</para>
///
/// <para>Las claves de ventanas pasadas se retiran al vuelo, cuando se toca esa misma clave, y no
/// con una tarea de limpieza: sin Redis no hay vencimiento automático, y un diccionario que solo
/// crece es una fuga con nombre de caché.</para>
/// </summary>
public sealed class LimitadorDePeticionesEnProceso : ILimitadorDePeticiones
{
    private readonly ConcurrentDictionary<string, Conteo> _conteos = new();

    private sealed record Conteo(long Ventana, long Consumidas);

    public Task<ResultadoDelLimite> RegistrarAsync(
        string clave, int cupo, TimeSpan ventana, CancellationToken ct = default)
    {
        var ahora = DateTimeOffset.UtcNow;
        var ticksDeVentana = Math.Max(1, ventana.Ticks);
        var numeroDeVentana = ahora.UtcTicks / ticksDeVentana;
        var restante = TimeSpan.FromTicks(((numeroDeVentana + 1) * ticksDeVentana) - ahora.UtcTicks);

        var actualizado = _conteos.AddOrUpdate(
            clave,
            _ => new Conteo(numeroDeVentana, 1),
            // Ventana distinta ⇒ el conteo anterior ya no cuenta: se reemplaza en vez de sumarse.
            (_, previo) => previo.Ventana == numeroDeVentana
                ? previo with { Consumidas = previo.Consumidas + 1 }
                : new Conteo(numeroDeVentana, 1));

        return Task.FromResult(new ResultadoDelLimite(
            Permitida: actualizado.Consumidas <= cupo,
            Consumidas: actualizado.Consumidas,
            EsperaSugerida: restante));
    }
}
