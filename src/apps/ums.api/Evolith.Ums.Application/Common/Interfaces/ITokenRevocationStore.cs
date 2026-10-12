namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// HARDENING-03: Tracks revoked user sessions.
///
/// When a user is deleted or blocked, their JWT may still be valid for up to the
/// token TTL (typically 15–60 minutes). This store allows the API to reject requests
/// from those users immediately, without waiting for token expiry.
///
/// Implementations:
/// - <see cref="InMemoryTokenRevocationStore"/> — single-node, dev/test, not durable.
/// - Redis-backed implementation (production) — shared across all pods, durable.
///   Replace registration in DI by switching to AddStackExchangeRedisCache() and
///   implementing ITokenRevocationStore against IDistributedCache.
///
/// Consumer: <c>TokenRevocationMiddleware</c> checks this on every authenticated request.
/// Producer: domain event handlers for <c>UserDeletedEvent</c> and <c>UserBlockedEvent</c>.
/// </summary>
public interface ITokenRevocationStore
{
    /// <summary>
    /// Marks the given user as revoked. Any request carrying their identity will be
    /// rejected until <paramref name="revokeUntilUtc"/> (set to token max lifetime +
    /// a safety margin, or DateTime.MaxValue for permanent revocation).
    /// </summary>
    Task RevokeAsync(string userId, DateTime revokeUntilUtc, CancellationToken ct = default);

    /// <summary>Returns true when the user ID is present in the revocation list and the entry has not expired.</summary>
    Task<bool> IsRevokedAsync(string userId, CancellationToken ct = default);

    /// <summary>Removes expired entries. Called periodically by a background task to prevent unbounded growth.</summary>
    Task PurgeExpiredAsync(CancellationToken ct = default);
}
