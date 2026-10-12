namespace Evolith.Ums.Domain.Configuration.Parameter;

using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;

public sealed class ParameterGlobalValueProps : IProps
{
    public IdValueObject Id { get; private set; }
    public IdValueObject ParameterDefinitionId { get; private set; }
    public EffectiveValue Value { get; private set; }
    public ConfigStatus Status { get; private set; }
    public string Version { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public ParameterGlobalValueProps(
        IdValueObject id,
        IdValueObject parameterDefinitionId,
        EffectiveValue value,
        ConfigStatus status,
        ActorId createdBy)
    {
        Id = id;
        ParameterDefinitionId = parameterDefinitionId;
        Value = value;
        Status = status;
        Version = "1.0.0";
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    private ParameterGlobalValueProps(
        IdValueObject id,
        IdValueObject parameterDefinitionId,
        EffectiveValue value,
        ConfigStatus status,
        string version,
        AuditValueObject audit)
    {
        Id = id;
        ParameterDefinitionId = parameterDefinitionId;
        Value = value;
        Status = status;
        Version = version;
        Audit = audit;
    }

    public ParameterGlobalValueProps WithValue(EffectiveValue value) => new(Id, ParameterDefinitionId, value, Status, Version, Audit);
    public ParameterGlobalValueProps WithStatus(ConfigStatus status) => new(Id, ParameterDefinitionId, Value, status, Version, Audit);
    public ParameterGlobalValueProps WithVersion(string version) => new(Id, ParameterDefinitionId, Value, Status, version, Audit);

    public object Clone() => MemberwiseClone();
}

public sealed record EffectiveValue
{
    public string Value { get; }

    private EffectiveValue(string value) => Value = value;

    public static EffectiveValue Create(string value) => new(value ?? string.Empty);

    public static EffectiveValue? TryCreate(string? value) =>
        value is null ? null : Create(value);

    public bool HasValue => !string.IsNullOrEmpty(Value);
}