namespace Evolith.Ums.Infrastructure.Persistence;

/// <summary>
/// Traduce errores del motor a preguntas de dominio, para que los repositorios no tengan que
/// conocer los códigos de cada proveedor.
/// </summary>
internal static class ErroresDePersistencia
{
    /// <summary>Número de <c>SqlException</c>: fila duplicada en un índice único.</summary>
    private const int SqlServerFilaDuplicadaEnIndiceUnico = 2601;

    /// <summary>Número de <c>SqlException</c>: violación de restricción PRIMARY KEY o UNIQUE.</summary>
    private const int SqlServerViolacionDeRestriccionUnica = 2627;

    /// <summary>
    /// <c>true</c> si la excepción interna de un <c>DbUpdateException</c> corresponde a una
    /// violación de unicidad.
    ///
    /// <para>PostgreSQL la señala con el SQLSTATE 23505. SQL Server no usa SQLSTATE: distingue
    /// entre 2601 (índice único) y 2627 (restricción PK/UNIQUE) según cómo se declarara la
    /// unicidad, y ambos significan lo mismo para quien llama, así que se aceptan los dos.</para>
    /// </summary>
    public static bool EsViolacionDeUnicidad(Exception? excepcionInterna) =>
        excepcionInterna switch
        {
            Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation } => true,
            Microsoft.Data.SqlClient.SqlException sql =>
                sql.Number is SqlServerFilaDuplicadaEnIndiceUnico or SqlServerViolacionDeRestriccionUnica,
            _ => false,
        };
}
