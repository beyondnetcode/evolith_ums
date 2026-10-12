using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence;

/// <summary>
/// Abstraction for database-level distributed locks, enabling the architecture
/// to support the PostgreSQL advisory-lock implementation (pg_advisory_lock).
/// without coupling the bootstrapper or outbox dispatcher to a specific database engine.
/// </summary>
public interface IDistributedLockProvider
{
    /// <summary>
    /// Acquires a distributed lock on the specified resource.
    /// The lock is released automatically when the returned IAsyncDisposable is disposed.
    /// </summary>
    Task<IAsyncDisposable> AcquireLockAsync(
        DbContext dbContext,
        string resourceName,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
