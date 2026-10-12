using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence;

public sealed class PostgreSqlDistributedLockProvider : IDistributedLockProvider
{
    public async Task<IAsyncDisposable> AcquireLockAsync(
        DbContext dbContext,
        string resourceName,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var timeoutMs = (int)timeout.TotalMilliseconds;

        // PostgreSQL uses advisory locks based on 64-bit integers.
        // We use a simple hash of the resource name.
        // We use pg_advisory_lock which is session-level.
        // LockTimeout is not natively supported by pg_advisory_lock in the same way,
        // so we just acquire it (blocking). Alternatively we could use set_config('lock_timeout').

        await dbContext.Database.ExecuteSqlRawAsync(
            "SELECT set_config('lock_timeout', {0}, true); SELECT pg_advisory_lock(hashtext({1}));",
            new object[] { timeoutMs.ToString(), resourceName },
            cancellationToken);

        return new PostgreSqlLockScope(dbContext, resourceName);
    }

    private sealed class PostgreSqlLockScope : IAsyncDisposable
    {
        private readonly DbContext _dbContext;
        private readonly string _resourceName;

        public PostgreSqlLockScope(DbContext dbContext, string resourceName)
        {
            _dbContext = dbContext;
            _resourceName = resourceName;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_unlock(hashtext({0}));",
                    new object[] { _resourceName });
            }
            catch
            {
                // Suppress release errors so we don't mask original exceptions during disposal.
                // The lock will naturally release when the connection is closed.
            }
        }
    }
}
