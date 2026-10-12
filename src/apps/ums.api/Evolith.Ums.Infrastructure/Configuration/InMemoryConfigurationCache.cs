namespace Evolith.Ums.Infrastructure.Configuration;

using System.Collections.Concurrent;
using Evolith.Ums.Application.Configuration.Services;
using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;

/// <summary>
/// In-process, thread-safe implementation of <see cref="IConfigurationCache"/>.
///
/// TODO(G-069): Replace this phase-1 in-memory cache with a Redis-backed implementation
/// that wraps IDistributedCache when distributed cache infrastructure is available.
/// No es coherente entre réplicas al escalar horizontalmente. Ver GAPS.md G-069.
///
/// Resolution order (BR-1): Module → Suite → Tenant → Global (most specific wins).
/// Each scope has its own dictionary keyed by the scope's natural ID:
///   _global    → code
///   _tenant    → tenantId → code
///   _suite     → systemSuiteId → code
///   _module    → moduleId → code
/// </summary>
public sealed class InMemoryConfigurationCache : IConfigurationCache
{
    private readonly ConcurrentDictionary<string, AppConfigurationAggregate> _global
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, AppConfigurationAggregate>> _tenant = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, AppConfigurationAggregate>> _suite  = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, AppConfigurationAggregate>> _module = new();

    // ── Read ────────────────────────────────────────────────────────────────

    public AppConfigurationAggregate? GetGlobal(string code)
    {
        _global.TryGetValue(code, out var config);
        return config;
    }

    public AppConfigurationAggregate? GetForTenant(Guid tenantId, string code)
        => TryGet(_tenant, tenantId, code);

    public AppConfigurationAggregate? GetForSuite(Guid suiteId, string code)
        => TryGet(_suite, suiteId, code);

    public AppConfigurationAggregate? GetForModule(Guid moduleId, string code)
        => TryGet(_module, moduleId, code);

    /// <summary>
    /// Full 4-level cascade: Module → Suite → Tenant → Global.
    /// </summary>
    public AppConfigurationAggregate? GetWithPrecedence(
        string code,
        Guid? tenantId,
        Guid? suiteId  = null,
        Guid? moduleId = null)
    {
        if (moduleId.HasValue)
        {
            var moduleValue = TryGet(_module, moduleId.Value, code);
            if (moduleValue is not null) return moduleValue;
        }

        if (suiteId.HasValue)
        {
            var suiteValue = TryGet(_suite, suiteId.Value, code);
            if (suiteValue is not null) return suiteValue;
        }

        if (tenantId.HasValue)
        {
            var tenantValue = TryGet(_tenant, tenantId.Value, code);
            if (tenantValue is not null) return tenantValue;
        }

        _global.TryGetValue(code, out var global);
        return global;
    }

    public IReadOnlyList<AppConfigurationAggregate> GetAllGlobal()
        => _global.Values.ToList();

    public IReadOnlyList<AppConfigurationAggregate> GetAllForTenant(Guid tenantId)
        => _tenant.TryGetValue(tenantId, out var cache) ? cache.Values.ToList() : [];

    public bool HasTenantOverride(string code, Guid tenantId)
        => TryGet(_tenant, tenantId, code) is not null;

    // ── Write ────────────────────────────────────────────────────────────────

    public void PopulateGlobal(IEnumerable<AppConfigurationAggregate> configs)
    {
        foreach (var config in configs)
            _global[config.Code.GetValue()] = config;
    }

    public void PopulateTenant(Guid tenantId, IEnumerable<AppConfigurationAggregate> configs)
        => PopulateScope(_tenant, tenantId, configs);

    public void PopulateSuite(Guid suiteId, IEnumerable<AppConfigurationAggregate> configs)
        => PopulateScope(_suite, suiteId, configs);

    public void PopulateModule(Guid moduleId, IEnumerable<AppConfigurationAggregate> configs)
        => PopulateScope(_module, moduleId, configs);

    public void InvalidateTenant(Guid tenantId) => _tenant.TryRemove(tenantId, out _);

    public void InvalidateSuite(Guid suiteId) => _suite.TryRemove(suiteId, out _);

    public void InvalidateModule(Guid moduleId) => _module.TryRemove(moduleId, out _);

    public void InvalidateAll()
    {
        _global.Clear();
        _tenant.Clear();
        _suite.Clear();
        _module.Clear();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AppConfigurationAggregate? TryGet(
        ConcurrentDictionary<Guid, ConcurrentDictionary<string, AppConfigurationAggregate>> store,
        Guid key,
        string code)
        => store.TryGetValue(key, out var inner) && inner.TryGetValue(code, out var config)
            ? config
            : null;

    private static void PopulateScope(
        ConcurrentDictionary<Guid, ConcurrentDictionary<string, AppConfigurationAggregate>> store,
        Guid key,
        IEnumerable<AppConfigurationAggregate> configs)
    {
        var bucket = store.GetOrAdd(key, _ => new ConcurrentDictionary<string, AppConfigurationAggregate>(StringComparer.OrdinalIgnoreCase));
        foreach (var config in configs)
            bucket[config.Code.GetValue()] = config;
    }
}
