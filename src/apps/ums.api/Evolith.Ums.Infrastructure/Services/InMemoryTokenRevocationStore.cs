using System.Collections.Concurrent;

namespace Evolith.Ums.Infrastructure.Services;

/// <summary>
/// HARDENING-03: InMemory token revocation store.
///
/// Suitable for single-node deployments, development, and integration tests.
/// For production multi-pod deployments replace with a Redis-backed implementation
/// that uses IDistributedCache so all pods share the revocation list.
///
/// Thread-safe via ConcurrentDictionary. Entries auto-expire via PurgeExpiredAsync
/// (called by a background task) to prevent unbounded memory growth.
/// </summary>
public sealed class InMemoryTokenRevocationStore : ITokenRevocationStore
{
    // Key: userId (string), Value: revocation expiry (UTC)
    private readonly ConcurrentDictionary<string, DateTime> _revoked = new(StringComparer.OrdinalIgnoreCase);

    public Task RevokeAsync(string userId, DateTime revokeUntilUtc, CancellationToken ct = default)
    {
        // Always take the furthest expiry to handle re-revocation after token refresh.
        _revoked.AddOrUpdate(userId, revokeUntilUtc, (_, existing) =>
            existing < revokeUntilUtc ? revokeUntilUtc : existing);

        return Task.CompletedTask;
    }

    public Task<bool> IsRevokedAsync(string userId, CancellationToken ct = default)
    {
        if (_revoked.TryGetValue(userId, out var until))
        {
            if (DateTime.UtcNow < until) return Task.FromResult(true);

            // Entry has expired — remove it lazily.
            _revoked.TryRemove(userId, out _);
        }

        return Task.FromResult(false);
    }

    public Task PurgeExpiredAsync(CancellationToken ct = default)
    {
        var now     = DateTime.UtcNow;
        var expired = _revoked.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList();
        foreach (var key in expired) _revoked.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
