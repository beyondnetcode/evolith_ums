namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// Resultado de contar una petición contra su cupo.
/// </summary>
/// <param name="Permitida">Falso cuando la petición excede el cupo de la ventana en curso.</param>
/// <param name="Consumidas">Peticiones contadas en la ventana, incluida esta.</param>
/// <param name="EsperaSugerida">Lo que falta para que la ventana se renueve. Viaja en `Retry-After`.</param>
public readonly record struct ResultadoDelLimite(bool Permitida, long Consumidas, TimeSpan EsperaSugerida);

/// <summary>
/// Cuenta peticiones por clave dentro de una ventana fija (G-248).
///
/// <para>POR QUÉ EXISTE. El limitador de ASP.NET es un <c>PartitionedRateLimiter</c> en proceso:
/// cada réplica cuenta las suyas sin saber de las demás, así que con N réplicas el cupo efectivo
/// es N veces el declarado. Con una réplica no se notaba; al escalar, la protección se diluye justo
/// cuando más hace falta, y en silencio.</para>
///
/// <para>La ventana es FIJA y no deslizante, igual que la que sustituye. Una deslizante es más
/// justa en el borde —con la fija, un cliente puede gastar el cupo al final de una ventana y otro
/// tanto al principio de la siguiente—, pero exige guardar cada marca de tiempo por clave en vez de
/// un contador. El comportamiento anterior era de ventana fija: cambiarlo aquí mezclaría dos
/// cambios en uno y haría imposible saber a cuál atribuir una diferencia de medición.</para>
/// </summary>
public interface ILimitadorDePeticiones
{
    /// <summary>Cuenta una petición de <paramref name="clave"/> y dice si cabe en el cupo.</summary>
    Task<ResultadoDelLimite> RegistrarAsync(
        string clave, int cupo, TimeSpan ventana, CancellationToken ct = default);
}
