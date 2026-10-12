namespace Evolith.Ums.Application.Configuration.Services;

using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.AppConfiguration;
using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;

public interface IConfigurationProvider
{
    AppConfigurationAggregate? GetGlobal(string code);

    AppConfigurationAggregate? GetForTenant(Guid tenantId, string code);

    /// <summary>Full cascade: Module → Suite → Tenant → Global.</summary>
    AppConfigurationAggregate? GetWithPrecedence(string code, Guid? tenantId = null, Guid? suiteId = null, Guid? moduleId = null);

    string GetValue(string code, Guid? tenantId = null, string? defaultValue = null);

    string GetValue(string code, Guid? tenantId, Guid? suiteId, Guid? moduleId, string? defaultValue = null);

    T GetValueAs<T>(string code, Guid? tenantId = null, T? defaultValue = default);

    T GetValueAs<T>(string code, Guid? tenantId, Guid? suiteId, Guid? moduleId, T? defaultValue = default);

    bool HasOverride(string code, Guid tenantId);

    IReadOnlyList<AppConfigurationAggregate> GetAllGlobal();

    IReadOnlyList<AppConfigurationAggregate> GetAllForTenant(Guid tenantId);

    void Set(string code, string value, Guid? tenantId = null);

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task ReloadAsync(CancellationToken cancellationToken = default);

    Task ReloadTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
}

public sealed class ConfigurationChangedEventArgs : EventArgs
{
    public string Code { get; }
    public Guid? TenantId { get; }
    public string? OldValue { get; }
    public string? NewValue { get; }
    public DateTime Timestamp { get; }

    public ConfigurationChangedEventArgs(string code, Guid? tenantId, string? oldValue, string? newValue)
    {
        Code = code;
        TenantId = tenantId;
        OldValue = oldValue;
        NewValue = newValue;
        Timestamp = DateTime.UtcNow;
    }
}