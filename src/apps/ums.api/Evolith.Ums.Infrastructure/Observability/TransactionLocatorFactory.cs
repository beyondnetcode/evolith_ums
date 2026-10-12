namespace Evolith.Ums.Infrastructure.Observability;

using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Infrastructure.Persistence;

/// <summary>
/// Acuña el localizador legible <c>TX-AAAA-NNNNNN</c> con una <b>secuencia PostgreSQL por
/// año</b> (ADR-UMS-084). La secuencia se crea de forma perezosa e idempotente
/// (<c>CREATE SEQUENCE IF NOT EXISTS</c>) y reinicia al cambiar de año, porque cada año
/// tiene su propia secuencia (<c>tx_locator_AAAA</c>). Las secuencias de PostgreSQL son
/// <b>no transaccionales</b>: <c>nextval</c> persiste aunque la transacción de la petición
/// se revierta, lo que da un localizador monotónico sin huecos reutilizables.
///
/// Fuera de un proveedor relacional (p. ej. el proveedor InMemory de las pruebas) se cae a
/// un contador en memoria del proceso, de modo que el formato del localizador se mantiene
/// verificable sin una base de datos real.
/// </summary>
public sealed class TransactionLocatorFactory : ITransactionLocatorFactory
{
    private static readonly ConcurrentDictionary<int, long> InMemoryCounters = new();

    private readonly UmsPlatformDbContext _dbContext;

    public TransactionLocatorFactory(UmsPlatformDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<string> NextAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTimeOffset.UtcNow.Year;
        var next = _dbContext.Database.IsRelational()
            ? await NextFromSequenceAsync(year, cancellationToken)
            : InMemoryCounters.AddOrUpdate(year, 1, static (_, current) => current + 1);

        return Format(year, next);
    }

    internal static string Format(int year, long sequence)
        => string.Create(CultureInfo.InvariantCulture, $"TX-{year:D4}-{sequence:D6}");

    private async Task<long> NextFromSequenceAsync(int year, CancellationToken cancellationToken)
    {
        // El nombre de la secuencia se compone de un entero validado (el año), no de
        // entrada del usuario: no hay superficie de inyección.
        var esPostgres = _dbContext.Database.IsNpgsql();
        var esquema = UmsPlatformDbContext.DefaultSchema;
        var nombreSecuencia = $"tx_locator_{year}";

        var sequenceName = esPostgres
            ? $"\"{esquema}\".\"{nombreSecuencia}\""
            : $"[{esquema}].[{nombreSecuencia}]";

        var connection = _dbContext.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;
        if (mustClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();

            var currentTransaction = _dbContext.Database.CurrentTransaction;
            if (currentTransaction is not null)
            {
                command.Transaction = currentTransaction.GetDbTransaction();
            }

            // S2077: el identificador de secuencia NO es parametrizable en SQL (los nombres de objeto no
            // admiten binding). sequenceName se compone de un entero validado (el año) y un esquema constante,
            // no de entrada del usuario (ver comentario superior): no hay superficie de inyección.
            //
            // SQL Server no tiene `CREATE SEQUENCE IF NOT EXISTS` ni `nextval(...)`: la existencia
            // se consulta en sys.sequences y el avance es `NEXT VALUE FOR`. Se arranca en 1 para
            // igualar el valor inicial por omisión de PostgreSQL, de modo que el localizador
            // TX-AAAA-NNNNNN numere igual en ambos motores.
#pragma warning disable S2077
            command.CommandText = esPostgres
                ? $"CREATE SEQUENCE IF NOT EXISTS {sequenceName}; SELECT nextval('{sequenceName}');"
                : $"""
                   IF NOT EXISTS (
                       SELECT 1 FROM sys.sequences s
                       JOIN sys.schemas e ON e.schema_id = s.schema_id
                       WHERE e.name = '{esquema}' AND s.name = '{nombreSecuencia}')
                       CREATE SEQUENCE {sequenceName} AS bigint START WITH 1 INCREMENT BY 1;
                   SELECT NEXT VALUE FOR {sequenceName};
                   """;
#pragma warning restore S2077

            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (mustClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
