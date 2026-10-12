namespace Evolith.Ums.Domain.Configuration.Parameter;

using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;

public sealed class ParameterTenantValueProps : IProps
{
    public IdValueObject Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public IdValueObject ParameterDefinitionId { get; private set; }
    public OverrideValue Value { get; private set; }
    public ConfigStatus Status { get; private set; }
    public string Version { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public ParameterTenantValueProps(
        IdValueObject id,
        TenantId tenantId,
        IdValueObject parameterDefinitionId,
        OverrideValue value,
        ConfigStatus status,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        ParameterDefinitionId = parameterDefinitionId;
        Value = value;
        Status = status;
        Version = "1.0.0";
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    private ParameterTenantValueProps(
        IdValueObject id,
        TenantId tenantId,
        IdValueObject parameterDefinitionId,
        OverrideValue value,
        ConfigStatus status,
        string version,
        AuditValueObject audit)
    {
        Id = id;
        TenantId = tenantId;
        ParameterDefinitionId = parameterDefinitionId;
        Value = value;
        Status = status;
        Version = version;
        Audit = audit;
    }

    public ParameterTenantValueProps WithValue(OverrideValue value) => new(Id, TenantId, ParameterDefinitionId, value, Status, Version, Audit);
    public ParameterTenantValueProps WithStatus(ConfigStatus status) => new(Id, TenantId, ParameterDefinitionId, Value, status, Version, Audit);
    public ParameterTenantValueProps WithVersion(string version) => new(Id, TenantId, ParameterDefinitionId, Value, Status, version, Audit);

    public object Clone() => MemberwiseClone();
}

public sealed record OverrideValue
{
    public string Value { get; }

    private OverrideValue(string value) => Value = value;

    public static OverrideValue Create(string value) => new(value ?? string.Empty);

    public static OverrideValue? TryCreate(string? value) =>
        value is null ? null : Create(value);

    public bool HasValue => !string.IsNullOrEmpty(Value);
}