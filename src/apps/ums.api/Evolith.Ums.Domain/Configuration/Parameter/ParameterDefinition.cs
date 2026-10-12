namespace Evolith.Ums.Domain.Configuration.Parameter;

using Evolith.Ums.Domain.Configuration.Parameter.ValueObjects;

public sealed class ParameterDefinition : AggregateRoot<ParameterDefinition, ParameterDefinitionProps>
{
    private ParameterDefinition(ParameterDefinitionProps props) : base(props)
    {
    }

    public Code Code => Props.Code;
    public ParameterName Name => Props.Name;
    public Description Description => Props.Description;
    public ParameterDataType DataType => Props.DataType;
    public DefaultValue DefaultValue => Props.DefaultValue;
    public ParameterScope Scope => Props.Scope;
    public bool IsActive => Props.IsActive;
    public bool IsMandatory => Props.IsMandatory;
    public int DisplayOrder => Props.DisplayOrder;
    public bool IsDeleted => Props.IsDeleted;
    public new string Version => Props.Version;

    public ParameterDefinitionId GetId() => ParameterDefinitionId.Load(Props.Id.GetValue());

    public static Result<ParameterDefinition> Create(
        Code code,
        ParameterName name,
        Description description,
        ParameterDataType dataType,
        DefaultValue defaultValue,
        ParameterScope scope,
        bool isActive,
        bool isMandatory,
        int displayOrder,
        ActorId createdBy,
        int existingDefinitionCount = 0)
    {
        if (code is null)
            return Result<ParameterDefinition>.Failure("Parameter code is required");

        if (name is null)
            return Result<ParameterDefinition>.Failure("Parameter name is required");

        if (defaultValue is null)
            return Result<ParameterDefinition>.Failure("Default value is required");

        if (existingDefinitionCount > 0)
        {
            return Result<ParameterDefinition>.Failure(DomainErrors.Configuration.ParameterCodeNotUnique);
        }

        var props = new ParameterDefinitionProps(
            IdValueObject.Create(), code, name, description,
            dataType, defaultValue, scope, isActive, isMandatory, displayOrder, createdBy);

        var entity = new ParameterDefinition(props);

        if (!entity.IsValid())
        {
            return Result<ParameterDefinition>.Failure(entity.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<ParameterDefinition>.Success(entity);
    }

    public Result Update(
        ParameterName name,
        Description description,
        DefaultValue defaultValue,
        ParameterScope scope,
        bool isActive,
        bool isMandatory,
        int displayOrder,
        ActorId updatedBy)
    {
        SetProps(Props
            .WithName(name)
            .WithDescription(description)
            .WithDefaultValue(defaultValue)
            .WithScope(scope)
            .WithIsActive(isActive)
            .WithIsMandatory(isMandatory)
            .WithDisplayOrder(displayOrder)
            .WithVersion(BumpMinorVersion(Props.Version)));

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Archive(ActorId updatedBy, int globalValueCount = 0, int tenantValueCount = 0)
    {
        if (globalValueCount > 0 || tenantValueCount > 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(Props.Id), DomainErrors.Configuration.ParameterHasActiveValues));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithIsActive(false).WithVersion(BumpMinorVersion(Props.Version)));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// Borrado LÓGICO de la definición (política del propietario: el borrado físico no existe).
    ///
    /// Regla transaccional, análoga al RESTRICT de una clave foránea: no se puede eliminar
    /// lógicamente algo que todavía tiene referencias REALES. Los contadores que recibe cuentan
    /// solo dependientes VIVOS —un valor ya eliminado lógicamente NO bloquea, porque su referencia
    /// dejó de ser real—. Se elige RESTRICT y no CASCADE: el actor decide qué se retira y en qué
    /// orden; la cascada silenciosa borraría configuración de inquilinos ajenos sin que nadie lo pida.
    ///
    /// Deja además `IsActive = false`: una definición borrada tampoco puede seguir resolviendo.
    /// </summary>
    public Result Delete(ActorId deletedBy, int liveGlobalValueCount = 0, int liveTenantValueCount = 0)
    {
        if (Props.IsDeleted)
        {
            BrokenRules.Add(new BrokenRule(nameof(Props.Id), DomainErrors.Configuration.ParameterAlreadyDeleted));
        }

        if (liveGlobalValueCount > 0 || liveTenantValueCount > 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(Props.Id), DomainErrors.Configuration.ParameterHasActiveValues));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props
            .WithIsDeleted(true)
            .WithIsActive(false)
            .WithVersion(BumpMinorVersion(Props.Version)));

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

public sealed class ParameterDefinitionId : IEquatable<ParameterDefinitionId>
{
    public Guid Value { get; }

    private ParameterDefinitionId(Guid value) => Value = value;

    public static ParameterDefinitionId Create(Guid value) => new(value);
    public static ParameterDefinitionId Load(Guid value) => new(value);
    public static ParameterDefinitionId New() => new(Guid.NewGuid());

    // G-055: identidad por valor. Antes de esto, dos instancias con el mismo Guid eran
    // distintas (Equals/GetHashCode por referencia), lo que rompe lookups, HashSet y
    // Dictionary. Toda la API pública (Value, Create, Load, New) se preserva intacta.
    public bool Equals(ParameterDefinitionId? other) => other is not null && Value.Equals(other.Value);

    public override bool Equals(object? obj) => Equals(obj as ParameterDefinitionId);

    public override int GetHashCode() => Value.GetHashCode();
}
