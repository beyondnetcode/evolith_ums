namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// Acuña el <b>localizador legible</b> de una transacción funcional en el formato
/// <c>TX-AAAA-NNNNNN</c> (ADR-0096 §2.2; decisión de implementación en ADR-UMS-084:
/// secuencia PostgreSQL por año). El localizador es comunicable por voz o chat y es lo
/// que el usuario pega en el ticket de soporte cuando algo falla.
/// </summary>
public interface ITransactionLocatorFactory
{
    /// <summary>
    /// Devuelve el siguiente localizador del año en curso. Cada llamada consume un valor
    /// de la secuencia (escritura barata, monotónica y no transaccional).
    /// </summary>
    Task<string> NextAsync(CancellationToken cancellationToken = default);
}
