namespace Evolith.Ums.Domain.Configuration.AppConfiguration;

public class AppConfigurationProps : IProps
{
    public IdValueObject Id { get; set; }
    public TenantId? TenantId { get; set; }
    public SystemSuiteId? SystemSuiteId { get; set; }
    public IdValueObject? ModuleId { get; set; }
    public Code Code { get; set; }
    public ConfigurationValue Value { get; set; }
    public Description Description { get; set; }
    public ConfigurationScope Scope { get; set; }
    public bool IsInheritable { get; set; }
    public bool IsEncrypted { get; set; }
    /// <summary>When true, lower-scope overrides for this code are blocked (BR-2).</summary>
    public bool IsNonOverridable { get; set; }
    public string Version { get; set; }
    public ConfigStatus Status { get; set; }
    public AuditValueObject Audit { get; private set; }

    public AppConfigurationProps(
        IdValueObject id,
        TenantId? tenantId,
        SystemSuiteId? systemSuiteId,
        IdValueObject? moduleId,
        Code code,
        ConfigurationValue value,
        Description description,
        bool isInheritable,
        bool isEncrypted,
        ActorId createdBy,
        bool isNonOverridable = false)
    {
        Id = id;
        TenantId = tenantId;
        SystemSuiteId = systemSuiteId;
        ModuleId = moduleId;
        Code = code;
        Value = value;
        Description = description;
        IsInheritable = isInheritable;
        IsEncrypted = isEncrypted;
        IsNonOverridable = isNonOverridable;
        Version = "1.0.0";
        Status = ConfigStatus.Draft;
        Scope = ResolveScope(tenantId, systemSuiteId, moduleId);
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    private static ConfigurationScope ResolveScope(TenantId? tenantId, SystemSuiteId? systemSuiteId, IdValueObject? moduleId)
    {
        if (moduleId is not null) return ConfigurationScope.Module;
        if (systemSuiteId is not null) return ConfigurationScope.Suite;
        if (tenantId is not null) return ConfigurationScope.Tenant;
        return ConfigurationScope.Global;
    }

    public object Clone() => MemberwiseClone();
}
