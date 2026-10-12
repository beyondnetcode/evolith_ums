namespace Evolith.Ums.Application.Identity.Auth;

using System.Diagnostics;

/// <summary>
/// Nivelador del tiempo de respuesta de los flujos anónimos que deben ser indistinguibles.
///
/// <para>Un cuerpo idéntico no basta para no filtrar la existencia de una cuenta: si el camino
/// «existe» escribe en la base y notifica, y el camino «no existe» retorna de inmediato, el reloj
/// dice lo que el mensaje calla. Este puerto acolcha la respuesta hasta un presupuesto fijo,
/// de modo que ambos caminos tarden lo mismo desde fuera.</para>
/// </summary>
public interface IResponseTimingNormalizer
{
    /// <summary>
    /// Espera lo que falte para completar el presupuesto desde <paramref name="startingTimestamp"/>
    /// (obtenido con <see cref="Stopwatch.GetTimestamp"/>). Si el trabajo real ya lo excedió, retorna
    /// sin esperar.
    /// </summary>
    Task NormalizeAsync(long startingTimestamp, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementación por reloj: acolcha hasta <see cref="Budget"/>.
/// </summary>
public sealed class ResponseTimingNormalizer : IResponseTimingNormalizer
{
    /// <summary>
    /// Presupuesto fijo. Debe superar con holgura el camino «cuenta existe» (lectura + escritura +
    /// notificación simulada) sin degradar la experiencia ni volverse una palanca de saturación:
    /// el endpoint es anónimo y cada petición retiene una conexión durante este tiempo.
    /// </summary>
    public static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(400);

    public async Task NormalizeAsync(long startingTimestamp, CancellationToken cancellationToken = default)
    {
        var remaining = Budget - Stopwatch.GetElapsedTime(startingTimestamp);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
        }
    }
}
