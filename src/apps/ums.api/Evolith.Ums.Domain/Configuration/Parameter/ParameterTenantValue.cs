namespace Evolith.Ums.Domain.Configuration.Parameter;

using Evolith.Ums.Domain.Configuration.Parameter.ValueObjects;

public sealed class ParameterTenantValue : AggregateRoot<ParameterTenantValue, ParameterTenantValueProps>
{
    private ParameterTenantValue(ParameterTenantValueProps props) : base(props)
    {
    }

    public TenantId TenantId => Props.TenantId;
    public IdValueObject ParameterDefinitionId => Props.ParameterDefinitionId;
    public OverrideValue Value => Props.Value;
    public ConfigStatus Status => Props.Status;
    public new string Version => Props.Version;

    public ParameterTenantValueId GetId() => ParameterTenantValueId.Load(Props.Id.GetValue());

    public static Result<ParameterTenantValue> Create(
        TenantId tenantId,
        IdValueObject parameterDefinitionId,
        OverrideValue value,
        ParameterDataType dataType,
        ParameterScope scope,
        ActorId createdBy)
    {
        if (tenantId is null)
            return Result<ParameterTenantValue>.Failure("Tenant ID is required");

        if (parameterDefinitionId is null)
            return Result<ParameterTenantValue>.Failure("Parameter definition ID is required");

        if (!scope.SupportsTenant)
        {
            return Result<ParameterTenantValue>.Failure(DomainErrors.Configuration.ParameterOverrideNotAllowed);
        }

        if (!ParameterValueValidation.IsValid(value?.Value, dataType))
        {
            return Result<ParameterTenantValue>.Failure(DomainErrors.Configuration.ParameterValueInvalidType);
        }

        var props = new ParameterTenantValueProps(
            IdValueObject.Create(),
            tenantId,
            parameterDefinitionId,
            value ?? OverrideValue.Create(string.Empty),
            ConfigStatus.Draft,
            createdBy);

        var entity = new ParameterTenantValue(props);

        if (!entity.IsValid())
        {
            return Result<ParameterTenantValue>.Failure(entity.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<ParameterTenantValue>.Success(entity);
    }

    public static Result<ParameterTenantValue> Create(
        TenantId tenantId,
        IdValueObject parameterDefinitionId,
        OverrideValue value,
        ActorId createdBy)
        => Create(tenantId, parameterDefinitionId, value, ParameterDataType.String, ParameterScope.GlobalAndTenant, createdBy);

    public Result UpdateValue(OverrideValue newValue, ParameterDataType dataType, ParameterScope scope, ActorId updatedBy)
    {
        if (Status == ConfigStatus.Archived)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), "Archived configuration cannot be modified"));
        }

        if (!scope.SupportsTenant)
        {
            BrokenRules.Add(new BrokenRule(nameof(ParameterDefinitionId), DomainErrors.Configuration.ParameterOverrideNotAllowed));
        }

        if (!ParameterValueValidation.IsValid(newValue?.Value, dataType))
        {
            BrokenRules.Add(new BrokenRule(nameof(Value), DomainErrors.Configuration.ParameterValueInvalidType));
        }

        SetProps(Props
            .WithValue(newValue ?? OverrideValue.Create(string.Empty))
            .WithVersion(BumpMinorVersion(Props.Version)));

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result UpdateValue(OverrideValue newValue, ActorId updatedBy)
        => UpdateValue(newValue, ParameterDataType.String, ParameterScope.GlobalAndTenant, updatedBy);

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
        if (Status != ConfigStatus.Published)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), "Only published configurations can be archived"));
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
    /// Borrado LÓGICO del override de inquilino (estado TERMINAL <see cref="ConfigStatus.Deleted"/>).
    ///
    /// Admisible desde cualquier estado vivo: es la vía para liberar la referencia que bloquea el
    /// borrado de la definición. La fila permanece; el override que estuvo vigente sigue siendo
    /// consultable para explicar el comportamiento pasado de ese inquilino.
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

public sealed class ParameterTenantValueId : IEquatable<ParameterTenantValueId>
{
    public Guid Value { get; }

    private ParameterTenantValueId(Guid value) => Value = value;

    public static ParameterTenantValueId Create(Guid value) => new(value);
    public static ParameterTenantValueId Load(Guid value) => new(value);
    public static ParameterTenantValueId New() => new(Guid.NewGuid());

    // G-055: identidad por valor. Antes de esto, dos instancias con el mismo Guid eran
    // distintas (Equals/GetHashCode por referencia), lo que rompe lookups, HashSet y
    // Dictionary. Toda la API pública (Value, Create, Load, New) se preserva intacta.
    public bool Equals(ParameterTenantValueId? other) => other is not null && Value.Equals(other.Value);

    public override bool Equals(object? obj) => Equals(obj as ParameterTenantValueId);

    public override int GetHashCode() => Value.GetHashCode();
}
