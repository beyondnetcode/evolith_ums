namespace Evolith.Ums.Domain.Identity.Tenant.TenantParameter;

using BeyondNetCode.Shell.Ddd;
using BeyondNetCode.Shell.Ddd.Rules.Impl;
using Evolith.Ums.Domain.Identity.Tenant.TenantParameter.Events;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using CodeEntity = Evolith.Ums.Domain.Kernel.ValueObjects.Code;
using DescriptionEntity = Evolith.Ums.Domain.Kernel.ValueObjects.Description;

public sealed class TenantParameter : AggregateRoot<TenantParameter, TenantParameterProps>
{
    public new TenantParameterDomainEventsManager DomainEvents { get; }

    private TenantParameter(TenantParameterProps props) : base(props)
    {
        DomainEvents = new TenantParameterDomainEventsManager(this);
        AddValidationRules();
    }

    public TenantParameterId GetId() => TenantParameterId.Load(Props.Id.GetValue());
    public TenantId TenantId => Props.TenantId;
    public CodeEntity Code => Props.Code;
    public DescriptionEntity Description => Props.Description;
    public string Value => Props.Value;
    public TenantParameterValueType ValueType => Props.ValueType;
    public TenantParameterCategory Category => Props.Category;
    public bool IsActive => Props.IsActive;
    /// <summary>Verdadero cuando el parámetro está lógicamente eliminado; las lecturas deben ocultarlo.</summary>
    public bool IsDeleted => Props.IsDeleted;
    public bool IsSensitive => Props.IsSensitive;
    public string? DefaultValue => Props.DefaultValue;
    public string? AllowedValues => Props.AllowedValues;

    internal bool ValidateValue(string value)
    {
        return Props.ValueType.Id switch
        {
            1 => !string.IsNullOrEmpty(value),
            2 => int.TryParse(value, out _),
            3 => bool.TryParse(value, out _),
            4 => !string.IsNullOrWhiteSpace(value),
            5 => true,
            _ => false
        };
    }

    public static Result<TenantParameter> Create(
        TenantId tenantId,
        string code,
        string description,
        string value,
        TenantParameterValueType valueType,
        TenantParameterCategory category,
        bool isSensitive,
        string? defaultValue,
        string? allowedValues,
        ActorId createdBy)
    {
        var props = new TenantParameterProps(
            IdValueObject.Create(),
            tenantId,
            CodeEntity.Create(code),
            DescriptionEntity.Create(description),
            value,
            valueType,
            category,
            true,
            isSensitive,
            defaultValue,
            allowedValues,
            AuditValueObject.Create(createdBy.GetValue()));

        var parameter = new TenantParameter(props);

        if (!parameter.IsValid())
        {
            return Result<TenantParameter>.Failure(parameter.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<TenantParameter>.Success(parameter);
    }

    public Result UpdateValue(string newValue, ActorId updatedBy)
    {
        if (!ValidateValue(newValue))
        {
            BrokenRules.Add(new BrokenRule(nameof(Value), DomainErrors.TenantParameter.InvalidValueType));
        }

        if (!string.IsNullOrEmpty(Props.AllowedValues))
        {
            var allowed = Props.AllowedValues.Split(',').Select(s => s.Trim()).ToArray();
            if (!allowed.Contains(newValue))
            {
                BrokenRules.Add(new BrokenRule(nameof(Value), DomainErrors.TenantParameter.ValueNotInAllowedList));
            }
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var oldValue = Props.Value;
        SetProps(Props.WithValue(newValue));
        Props.Audit.Update(updatedBy.GetValue());
        TrackingState.MarkAsDirty();
        DomainEvents.RaiseEvent(new TenantParameterUpdatedEvent(Props.TenantId.GetValue(), GetId().GetValue(), Props.Code.GetValue(), oldValue, newValue));
        return Result.Success();
    }

    public Result Deactivate(ActorId updatedBy)
    {
        if (IsDeleted)
        {
            // Un parámetro eliminado ya no admite transiciones: su ciclo de vida terminó.
            BrokenRules.Add(new BrokenRule(nameof(IsDeleted), DomainErrors.TenantParameter.AlreadyDeleted));
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithIsActive(false));
        Props.Audit.Update(updatedBy.GetValue());
        TrackingState.MarkAsDirty();
        DomainEvents.RaiseEvent(new TenantParameterDeactivatedEvent(Props.TenantId.GetValue(), GetId().GetValue(), Props.Code.GetValue()));
        return Result.Success();
    }

    public Result Reactivate(ActorId updatedBy)
    {
        if (IsDeleted)
        {
            // El borrado lógico es TERMINAL: reactivar no puede ser una puerta trasera para resucitar
            // un parámetro eliminado. Si el inquilino vuelve a necesitar ese código, se da de alta uno
            // nuevo y el eliminado queda como histórico consultable.
            BrokenRules.Add(new BrokenRule(nameof(IsDeleted), DomainErrors.TenantParameter.AlreadyDeleted));
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithIsActive(true));
        Props.Audit.Update(updatedBy.GetValue());
        TrackingState.MarkAsDirty();
        DomainEvents.RaiseEvent(new TenantParameterReactivatedEvent(Props.TenantId.GetValue(), GetId().GetValue(), Props.Code.GetValue()));
        return Result.Success();
    }

    /// <summary>
    /// Borrado LÓGICO: marca el parámetro como eliminado sin quitar la fila de la base. Solo existe
    /// borrado lógico porque el negocio consulta la configuración histórica de un inquilino (qué valor
    /// regía en una fecha dada) y un DELETE real la perdería para siempre.
    ///
    /// Regla de cascada: un parámetro ACTIVO es una referencia VIVA —la configuración del inquilino lo
    /// resuelve ahora mismo por su código a través de <c>ITenantParameterProvider</c>— y no se puede
    /// eliminar mientras lo sea. Hay que desactivarlo antes con <see cref="Deactivate"/>: esa
    /// desactivación ES la eliminación lógica del vínculo, y una vez hecha el borrado sí procede.
    /// </summary>
    public Result Delete(ActorId deletedBy)
    {
        if (IsDeleted)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsDeleted), DomainErrors.TenantParameter.AlreadyDeleted));
        }
        else if (IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.TenantParameter.HasActiveBinding));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithIsDeleted(true));
        Props.Audit.Update(deletedBy.GetValue());
        TrackingState.MarkAsDirty();
        DomainEvents.RaiseEvent(new TenantParameterDeletedEvent(Props.TenantId.GetValue(), GetId().GetValue(), Props.Code.GetValue()));
        return Result.Success();
    }

    public string GetTypedValue()
    {
        return Props.Value;
    }

    public bool GetBoolValue(bool defaultValue = false)
    {
        if (Props.ValueType.Id != 3)
            return defaultValue;

        return bool.TryParse(Props.Value, out var result) ? result : defaultValue;
    }

    public int GetIntValue(int defaultValue = 0)
    {
        if (Props.ValueType.Id != 2)
            return defaultValue;

        return int.TryParse(Props.Value, out var result) ? result : defaultValue;
    }

    public string[] GetStringListValue(string[] defaultValue = null!)
    {
        if (Props.ValueType.Id != 4)
            return defaultValue;

        if (string.IsNullOrWhiteSpace(Props.Value))
            return defaultValue;

        return Props.Value.Split(',').Select(s => s.Trim()).ToArray();
    }

    private void AddValidationRules()
    {
        ValidatorRules.Add(new TenantParameterCodeValidator(this));
    }
}

public class TenantParameterCodeValidator : AbstractRuleValidator<TenantParameter>
{
    public TenantParameterCodeValidator(TenantParameter subject) : base(subject) { }

    public override void AddRules(BeyondNetCode.Shell.Ddd.Rules.RuleContext? context)
    {
        if (string.IsNullOrWhiteSpace(Subject.Code.GetValue()))
        {
            AddBrokenRule(nameof(TenantParameter.Code), DomainErrors.Common.Required);
        }
    }
}