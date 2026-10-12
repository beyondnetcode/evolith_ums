using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence;

/// <summary>
/// Equivalente en SQL Server de <see cref="PostgreSqlDistributedLockProvider"/>, sobre
/// <c>sp_getapplock</c> / <c>sp_releaseapplock</c>.
///
/// <para>DIFERENCIA CON POSTGRESQL. <c>pg_advisory_lock</c> toma un entero de 64 bits, de ahí el
/// <c>hashtext(...)</c> del lado Postgres; <c>sp_getapplock</c> recibe el nombre del recurso tal
/// cual (hasta 255 caracteres), así que no hace falta hashear y desaparece el riesgo —remoto pero
/// real— de que dos recursos distintos colisionen en el mismo hash.</para>
///
/// <para><c>@LockOwner = 'Session'</c> reproduce el alcance de sesión del lado Postgres. Con el
/// valor por defecto (<c>'Transaction'</c>) el bloqueo exigiría una transacción abierta y se
/// liberaría en el commit, que no es el contrato de <see cref="IDistributedLockProvider"/>: aquí
/// lo libera el <c>DisposeAsync</c> del ámbito devuelto.</para>
/// </summary>
public sealed class SqlServerDistributedLockProvider : IDistributedLockProvider
{
    public async Task<IAsyncDisposable> AcquireLockAsync(
        DbContext dbContext,
        string resourceName,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var timeoutMs = (int)timeout.TotalMilliseconds;

        // sp_getapplock devuelve >= 0 si concede el bloqueo; los negativos son fallo
        // (-1 timeout, -2 cancelado, -3 víctima de interbloqueo, -999 parámetro inválido).
        // A diferencia de pg_advisory_lock, que espera indefinidamente, aquí el timeout SÍ es
        // nativo: se respeta el que pide quien llama en vez de ignorarlo.
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DECLARE @resultado int;
            EXEC @resultado = sp_getapplock
                @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = {1};
            IF @resultado < 0
                THROW 51000, 'No se pudo adquirir el bloqueo distribuido en SQL Server.', 1;
            """,
            [resourceName, timeoutMs],
            cancellationToken);

        return new SqlServerLockScope(dbContext, resourceName);
    }

    private sealed class SqlServerLockScope : IAsyncDisposable
    {
        private readonly DbContext _dbContext;
        private readonly string _resourceName;

        public SqlServerLockScope(DbContext dbContext, string resourceName)
        {
            _dbContext = dbContext;
            _resourceName = resourceName;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync(
                    "EXEC sp_releaseapplock @Resource = {0}, @LockOwner = 'Session';",
                    [_resourceName]);
            }
            catch
            {
                // Se silencian los errores de liberación para no enmascarar la excepción original
                // durante el descarte. El bloqueo cae solo al cerrarse la conexión.
            }
        }
    }
}
