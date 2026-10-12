namespace Evolith.Ums.Domain.Configuration.IdpConfiguration;

public class IdpConfigurationProps : IProps
{
    public IdValueObject Id { get; set; }
    public TenantId TenantId { get; set; }
    public SystemSuiteId SystemSuiteId { get; set; }
    public ProviderType ProviderType { get; set; }
    public string[] DomainHints { get; set; }
    public string ConfigPayload { get; set; }
    public string SecretRef { get; set; }
    public IdpConfigStatus Status { get; set; }
    public int ResolutionPriority { get; set; }
    public Guid? FallbackToId { get; set; }
    public int Version { get; set; }
    public AuditValueObject Audit { get; private set; }

    public IdpConfigurationProps(
        IdValueObject id,
        TenantId tenantId,
        SystemSuiteId systemSuiteId,
        ProviderType providerType,
        string[] domainHints,
        string configPayload,
        string secretRef,
        int resolutionPriority,
        Guid? fallbackToId,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        SystemSuiteId = systemSuiteId;
        ProviderType = providerType;
        DomainHints = domainHints;
        ConfigPayload = configPayload;
        SecretRef = secretRef;
        Status = IdpConfigStatus.Draft;
        ResolutionPriority = resolutionPriority;
        FallbackToId = fallbackToId;
        Version = 1;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public object Clone() => MemberwiseClone();
}
