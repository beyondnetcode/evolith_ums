namespace Evolith.Ums.Infrastructure.Configuration;

using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;

/// <summary>
/// Singleton implementation of <see cref="IConfigurationProvider"/>.
///
/// Delegates all storage to <see cref="IConfigurationCache"/> (TD-003: today in-memory,
/// future Redis). ConfigurationProvider handles load/reload orchestration and precedence
/// resolution; the cache handles the actual storage.
/// </summary>
public sealed class ConfigurationProvider : IConfigurationProvider, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfigurationCache _cache;
    private readonly IValueEncryptionService _encryption;
    private bool _isLoaded;
    private readonly object _loadLock = new();

    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    public ConfigurationProvider(
        IServiceScopeFactory scopeFactory,
        IConfigurationCache cache,
        IValueEncryptionService encryption)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _encryption = encryption;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        lock (_loadLock)
        {
            if (_isLoaded) return;
        }

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAppConfigurationRepository>();

        var allConfigs = await repository.GetAllAsync(null, cancellationToken);

        // Only Published configs participate in runtime resolution (BR-1, AC-3).
        _cache.PopulateGlobal(allConfigs
            .Where(c => c.Scope.Id == 1 && c.Props.Status.Id == 2)
            .Select(DecryptIfNeeded));

        // Suite (scope 4) and Module (scope 5) configs are cross-tenant by nature and
        // typically carry no TenantId. The per-tenant load below only reaches configs that
        // belong to a tenant, so these tenant-less scoped configs would otherwise never be
        // bucketed and would silently drop out of precedence resolution (G-048). Bucket them
        // first so any tenant-specific override can still win afterwards.
        BucketTenantlessScopedConfigs(allConfigs.Where(c => c.Props.TenantId is null));

        // Populate per-tenant entries — scope 2 = Tenant, 4 = Suite, 5 = Module (ConfigurationScope).
        var tenantIds = allConfigs
            .Where(c => c.Props.TenantId is not null)
            .Select(c => c.Props.TenantId!.GetValue())
            .Distinct();

        foreach (var tenantId in tenantIds)
        {
            var tenantConfigs = await repository.GetAllAsync(tenantId, cancellationToken);
            BucketTenantConfigs(tenantId, tenantConfigs);
        }

        lock (_loadLock)
        {
            _isLoaded = true;
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        _cache.InvalidateAll();

        lock (_loadLock)
        {
            _isLoaded = false;
        }

        await LoadAsync(cancellationToken);
    }

    public async Task ReloadTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAppConfigurationRepository>();

        // Fetch ALL statuses first so we can collect every suiteId/moduleId for this tenant,
        // including archived or draft ones that may need to be evicted from the cache.
        var allTenantConfigs = await repository.GetAllAsync(tenantId, cancellationToken);

        // Evict all scope buckets that belong to this tenant before repopulating.
        _cache.InvalidateTenant(tenantId);
        foreach (var suiteId in allTenantConfigs
                     .Where(c => c.Scope.Id == 4 && c.Props.SystemSuiteId is not null)
                     .Select(c => c.Props.SystemSuiteId!.GetValue())
                     .Distinct())
            _cache.InvalidateSuite(suiteId);

        foreach (var moduleId in allTenantConfigs
                     .Where(c => c.Scope.Id == 5 && c.Props.ModuleId is not null)
                     .Select(c => c.Props.ModuleId!.GetValue())
                     .Distinct())
            _cache.InvalidateModule(moduleId);

        // Repopulate with only Published configs (BR-1: only active values participate in resolution).
        BucketTenantConfigs(tenantId, allTenantConfigs);
    }

    // ── Reads (delegate to cache) ─────────────────────────────────────────────

    public AppConfigurationAggregate? GetGlobal(string code)
        => _cache.GetGlobal(code);

    public AppConfigurationAggregate? GetForTenant(Guid tenantId, string code)
        => _cache.GetForTenant(tenantId, code);

    public AppConfigurationAggregate? GetWithPrecedence(string code, Guid? tenantId = null, Guid? suiteId = null, Guid? moduleId = null)
        => _cache.GetWithPrecedence(code, tenantId, suiteId, moduleId);

    public string GetValue(string code, Guid? tenantId = null, string? defaultValue = null)
    {
        var config = GetWithPrecedence(code, tenantId);
        return config?.Props.Value.GetValue() ?? defaultValue ?? string.Empty;
    }

    public string GetValue(string code, Guid? tenantId, Guid? suiteId, Guid? moduleId, string? defaultValue = null)
    {
        var config = GetWithPrecedence(code, tenantId, suiteId, moduleId);
        return config?.Props.Value.GetValue() ?? defaultValue ?? string.Empty;
    }

    public T GetValueAs<T>(string code, Guid? tenantId = null, T? defaultValue = default)
    {
        var value = GetValue(code, tenantId);
        if (string.IsNullOrEmpty(value) && defaultValue is not null)
            return defaultValue;

        try
        {
            if (typeof(T) == typeof(int))    return (T)(object)int.Parse(value);
            if (typeof(T) == typeof(bool))   return (T)(object)bool.Parse(value);
            if (typeof(T) == typeof(double)) return (T)(object)double.Parse(value);
            if (typeof(T) == typeof(Guid))   return (T)(object)Guid.Parse(value);
            if (typeof(T) == typeof(string)) return (T)(object)value;
            return defaultValue ?? throw new NotSupportedException($"Type {typeof(T)} is not supported");
        }
        catch
        {
            if (defaultValue is not null) return defaultValue;
            throw;
        }
    }

    public T GetValueAs<T>(string code, Guid? tenantId, Guid? suiteId, Guid? moduleId, T? defaultValue = default)
    {
        var value = GetValue(code, tenantId, suiteId, moduleId);
        if (string.IsNullOrEmpty(value) && defaultValue is not null)
            return defaultValue;

        try
        {
            if (typeof(T) == typeof(int))    return (T)(object)int.Parse(value);
            if (typeof(T) == typeof(bool))   return (T)(object)bool.Parse(value);
            if (typeof(T) == typeof(double)) return (T)(object)double.Parse(value);
            if (typeof(T) == typeof(Guid))   return (T)(object)Guid.Parse(value);
            if (typeof(T) == typeof(string)) return (T)(object)value;
            return defaultValue ?? throw new NotSupportedException($"Type {typeof(T)} is not supported");
        }
        catch
        {
            if (defaultValue is not null) return defaultValue;
            throw;
        }
    }

    public bool HasOverride(string code, Guid tenantId)
        => _cache.HasTenantOverride(code, tenantId);

    public IReadOnlyList<AppConfigurationAggregate> GetAllGlobal()
        => _cache.GetAllGlobal();

    public IReadOnlyList<AppConfigurationAggregate> GetAllForTenant(Guid tenantId)
        => _cache.GetAllForTenant(tenantId);

    public void Set(string code, string value, Guid? tenantId = null)
    {
        var oldValue = GetValue(code, tenantId);
        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(code, tenantId, oldValue, value));
    }

    /// <summary>
    /// No invalida nada. La caché vive y muere con el proceso: vaciar sus diccionarios al
    /// apagar no libera nada que el recolector no vaya a liberar igual.
    ///
    /// Antes llamaba a <c>InvalidateAll()</c>, y con la caché distribuida activa eso publicaba
    /// un aviso de invalidación total: CADA apagado de pod en un despliegue progresivo forzaba
    /// una recarga completa de configuración en todos los pods restantes, justo cuando el resto
    /// del clúster absorbe el tráfico del que se va (G-170).
    /// </summary>
    public void Dispose()
    {
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Buckets a set of tenant configs into the correct scope caches,
    /// filtering to only Published entries (status.Id == 2) so Draft and
    /// Archived values never participate in runtime resolution.
    /// </summary>
    private void BucketTenantConfigs(Guid tenantId, IReadOnlyList<AppConfigurationAggregate> configs)
    {
        // Only Published configs; decrypt encrypted values so the runtime resolver gets plaintext.
        var published = configs
            .Where(c => c.Props.Status.Id == 2)
            .Select(DecryptIfNeeded)
            .ToList();

        _cache.PopulateTenant(tenantId, published.Where(c => c.Scope.Id == 2));

        foreach (var grp in published.Where(c => c.Scope.Id == 4 && c.Props.SystemSuiteId is not null)
                                     .GroupBy(c => c.Props.SystemSuiteId!.GetValue()))
            _cache.PopulateSuite(grp.Key, grp);

        foreach (var grp in published.Where(c => c.Scope.Id == 5 && c.Props.ModuleId is not null)
                                     .GroupBy(c => c.Props.ModuleId!.GetValue()))
            _cache.PopulateModule(grp.Key, grp);
    }

    /// <summary>
    /// Buckets Suite (scope 4) and Module (scope 5) configs that carry no TenantId.
    /// These cross-tenant overrides are otherwise never populated by the per-tenant load,
    /// which would silently drop them from precedence resolution (G-048). Only Published
    /// entries participate (BR-1); encrypted values are decrypted to plaintext.
    /// </summary>
    private void BucketTenantlessScopedConfigs(IEnumerable<AppConfigurationAggregate> configs)
    {
        var published = configs
            .Where(c => c.Props.Status.Id == 2)
            .Select(DecryptIfNeeded)
            .ToList();

        foreach (var grp in published.Where(c => c.Scope.Id == 4 && c.Props.SystemSuiteId is not null)
                                     .GroupBy(c => c.Props.SystemSuiteId!.GetValue()))
            _cache.PopulateSuite(grp.Key, grp);

        foreach (var grp in published.Where(c => c.Scope.Id == 5 && c.Props.ModuleId is not null)
                                     .GroupBy(c => c.Props.ModuleId!.GetValue()))
            _cache.PopulateModule(grp.Key, grp);
    }

    /// <summary>
    /// Returns a copy of the aggregate with the value decrypted when IsEncrypted=true.
    /// Used during cache population so the runtime resolver always works with plaintext.
    /// </summary>
    private AppConfigurationAggregate DecryptIfNeeded(AppConfigurationAggregate config)
    {
        if (!config.Props.IsEncrypted) return config;

        var raw = config.Props.Value.GetValue();
        if (!_encryption.IsEncryptedValue(raw)) return config;

        config.Props.Value = ConfigurationValue.Create(_encryption.Decrypt(raw));
        return config;
    }
}
