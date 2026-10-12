using Microsoft.Extensions.Caching.Distributed;

namespace Evolith.Ums.Infrastructure.Services;

/// <summary>
/// OPS-01: Redis-backed token revocation store.
///
/// Uses IDistributedCache (StackExchangeRedis under the hood) so every pod in a
/// multi-node deployment shares the same revocation list. Redis handles TTL-based
/// expiry automatically — no background purge job required.
///
/// Key schema:  revoke:{userId}
/// Value:       ISO-8601 UTC timestamp of the revocation expiry (for auditability).
/// TTL:         set to the remaining duration until revokeUntilUtc so Redis
///              auto-expires the entry without any application-level cleanup.
///
/// Registration:
///   services.AddStackExchangeRedisCache(o => o.Configuration = config["Redis:Connection"]);
///   services.AddSingleton&lt;ITokenRevocationStore, RedisTokenRevocationStore&gt;();
/// </summary>
public sealed class RedisTokenRevocationStore(IDistributedCache cache) : ITokenRevocationStore
{
    private static string CacheKey(string userId) => $"revoke:{userId}";

    /// <inheritdoc />
    public async Task RevokeAsync(string userId, DateTime revokeUntilUtc, CancellationToken ct = default)
    {
        var ttl = revokeUntilUtc - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero) return;   // Already in the past — nothing to store.

        // Store the expiry timestamp so ops tooling can read the value and understand
        // when the revocation was set to expire (helps debug revocation issues).
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = revokeUntilUtc,
        };

        await cache.SetStringAsync(CacheKey(userId), revokeUntilUtc.ToString("O"), options, ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsRevokedAsync(string userId, CancellationToken ct = default)
    {
        // GetStringAsync returns null when the key doesn't exist OR when Redis has expired it.
        var value = await cache.GetStringAsync(CacheKey(userId), ct);
        return value is not null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Redis expires keys automatically via the TTL set in RevokeAsync.
    /// This method is a no-op for the Redis implementation; it exists so the
    /// application-level background purge job doesn't need to special-case stores.
    /// </remarks>
    public Task PurgeExpiredAsync(CancellationToken ct = default) => Task.CompletedTask;
}
