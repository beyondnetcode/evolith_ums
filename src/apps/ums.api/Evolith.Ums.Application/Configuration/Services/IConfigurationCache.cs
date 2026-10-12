namespace Evolith.Ums.Application.Configuration.Services;

using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;

/// <summary>
/// Storage abstraction for in-process configuration cache.
///
/// TD-003: Introducing this interface from day one lets us swap the concrete
/// implementation from <see cref="InMemoryConfigurationCache"/> to a Redis-backed
/// implementation (RedisConfigurationCache) without changing <see cref="IConfigurationProvider"/>
/// or any business logic. The migration becomes a single DI registration change.
///
/// Design contract:
///  - All implementations must be thread-safe.
///  - Reads must be synchronous (no round-trip to an external store at read time).
///  - Writes (population / invalidation) may be asynchronous in future Redis implementations.
///  - The cache is loaded eagerly at startup and invalidated on parameter changes.
/// </summary>
public interface IConfigurationCache
{
    // ── Read ────────────────────────────────────────────────────────────────

    AppConfigurationAggregate? GetGlobal(string code);

    AppConfigurationAggregate? GetForTenant(Guid tenantId, string code);

    /// <summary>
    /// Returns the most specific value for the given scope chain, cascading
    /// Module → Suite → Tenant → Global (most specific wins, BR-1).
    /// Pass null for scopes that are not available in the current call context.
    /// </summary>
    AppConfigurationAggregate? GetWithPrecedence(string code, Guid? tenantId, Guid? suiteId = null, Guid? moduleId = null);

    AppConfigurationAggregate? GetForSuite(Guid suiteId, string code);
    AppConfigurationAggregate? GetForModule(Guid moduleId, string code);

    IReadOnlyList<AppConfigurationAggregate> GetAllGlobal();

    IReadOnlyList<AppConfigurationAggregate> GetAllForTenant(Guid tenantId);

    bool HasTenantOverride(string code, Guid tenantId);

    // ── Write (population / invalidation) ───────────────────────────────────

    void PopulateGlobal(IEnumerable<AppConfigurationAggregate> configs);

    void PopulateTenant(Guid tenantId, IEnumerable<AppConfigurationAggregate> configs);

    void PopulateSuite(Guid suiteId, IEnumerable<AppConfigurationAggregate> configs);

    void PopulateModule(Guid moduleId, IEnumerable<AppConfigurationAggregate> configs);

    /// <summary>Hot-reload: evicts all entries for a single tenant.</summary>
    void InvalidateTenant(Guid tenantId);

    void InvalidateSuite(Guid suiteId);

    void InvalidateModule(Guid moduleId);

    /// <summary>Full reload: evicts all entries.</summary>
    void InvalidateAll();
}
