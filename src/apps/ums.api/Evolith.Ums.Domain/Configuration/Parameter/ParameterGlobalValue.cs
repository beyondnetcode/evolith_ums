namespace Evolith.Ums.Domain.Configuration.Parameter;

using Evolith.Ums.Domain.Configuration.Parameter.ValueObjects;

public sealed class ParameterGlobalValue : AggregateRoot<ParameterGlobalValue, ParameterGlobalValueProps>
{
    private ParameterGlobalValue(ParameterGlobalValueProps props) : base(props)
    {
    }

    public IdValueObject ParameterDefinitionId => Props.ParameterDefinitionId;
    public EffectiveValue Value => Props.Value;
    public ConfigStatus Status => Props.Status;
    public new string Version => Props.Version;

    public ParameterGlobalValueId GetId() => ParameterGlobalValueId.Load(Props.Id.GetValue());

    public static Result<ParameterGlobalValue> Create(
        IdValueObject parameterDefinitionId,
        EffectiveValue value,
        ParameterDataType dataType,
        ActorId createdBy)
    {
        if (parameterDefinitionId is null)
            return Result<ParameterGlobalValue>.Failure("Parameter definition ID is required");

        if (!ParameterValueValidation.IsValid(value?.Value, dataType))
        {
            return Result<ParameterGlobalValue>.Failure(DomainErrors.Configuration.ParameterValueInvalidType);
        }

        var props = new ParameterGlobalValueProps(
            IdValueObject.Create(),
            parameterDefinitionId,
            value ?? EffectiveValue.Create(string.Empty),
            ConfigStatus.Draft,
            createdBy);

        var entity = new ParameterGlobalValue(props);

        if (!entity.IsValid())
        {
            return Result<ParameterGlobalValue>.Failure(entity.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<ParameterGlobalValue>.Success(entity);
    }

    public static Result<ParameterGlobalValue> Create(
        IdValueObject parameterDefinitionId,
        EffectiveValue value,
        ActorId createdBy)
        => Create(parameterDefinitionId, value, ParameterDataType.String, createdBy);

    public Result UpdateValue(EffectiveValue newValue, ParameterDataType dataType, ActorId updatedBy)
    {
        if (Status == ConfigStatus.Archived)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), "Archived configuration cannot be modified"));
        }

        if (!ParameterValueValidation.IsValid(newValue?.Value, dataType))
        {
            BrokenRules.Add(new BrokenRule(nameof(Value), DomainErrors.Configuration.ParameterValueInvalidType));
        }

        SetProps(Props
            .WithValue(newValue ?? EffectiveValue.Create(string.Empty))
            .WithVersion(BumpMinorVersion(Props.Version)));

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result UpdateValue(EffectiveValue newValue, ActorId updatedBy)
        => UpdateValue(newValue, ParameterDataType.String, updatedBy);

    public Result Publish(ActorId updatedBy)
    {
        if (Status != ConfigStatus.Draft)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), "Only draft configurations can be published"));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props
            .WithStatus(ConfigStatus.Published)
            .WithVersion(BumpMinorVersion(Props.Version)));

        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Archive(ActorId updatedBy)
    {
        return Archive(updatedBy, 0);
    }

    public Result Archive(ActorId updatedBy, int activeTenantValueCount)
    {
        if (Status != ConfigStatus.Published)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), "Only published configurations can be archived"));
        }

        if (activeTenantValueCount > 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(ParameterDefinitionId), DomainErrors.Configuration.ParameterGlobalValueInUse));
        }

        SetProps(Props.WithStatus(ConfigStatus.Archived));

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// Borrado LÓGICO del valor global (estado TERMINAL <see cref="ConfigStatus.Deleted"/>).
    ///
    /// Admisible desde cualquier estado vivo —Draft, Published o Archived—: es la única forma de
    /// liberar la referencia que bloquea el borrado de su definición. La fila permanece: el valor
    /// que estuvo vigente sigue explicando el comportamiento pasado del sistema.
    /// </summary>
    public Result Delete(ActorId deletedBy)
    {
        if (Status == ConfigStatus.Deleted)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), "Parameter value is already deleted"));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(ConfigStatus.Deleted));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(deletedBy.GetValue());
        return Result.Success();
    }

    private static string BumpMinorVersion(string semver)
    {
        if (System.Version.TryParse(semver, out var v))
            return $"{v.Major}.{v.Minor + 1}.0";
        return semver;
    }
}

public sealed class ParameterGlobalValueId : IEquatable<ParameterGlobalValueId>
{
    public Guid Value { get; }

    private ParameterGlobalValueId(Guid value) => Value = value;

    public static ParameterGlobalValueId Create(Guid value) => new(value);
    public static ParameterGlobalValueId Load(Guid value) => new(value);
    public static ParameterGlobalValueId New() => new(Guid.NewGuid());

    // G-055: identidad por valor. Antes de esto, dos instancias con el mismo Guid eran
    // distintas (Equals/GetHashCode por referencia), lo que rompe lookups, HashSet y
    // Dictionary. Toda la API pública (Value, Create, Load, New) se preserva intacta.
    public bool Equals(ParameterGlobalValueId? other) => other is not null && Value.Equals(other.Value);

    public override bool Equals(object? obj) => Equals(obj as ParameterGlobalValueId);

    public override int GetHashCode() => Value.GetHashCode();
}
