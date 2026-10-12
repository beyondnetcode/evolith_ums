namespace Evolith.Ums.Domain.Identity.Tenant.TenantParameter;

using Evolith.Ums.Domain.Kernel.ValueObjects;
using CodeEntity = Evolith.Ums.Domain.Kernel.ValueObjects.Code;
using DescriptionEntity = Evolith.Ums.Domain.Kernel.ValueObjects.Description;

public class TenantParameterProps : IProps
{
    public TenantParameterProps(
        IdValueObject id,
        TenantId tenantId,
        CodeEntity code,
        DescriptionEntity description,
        string value,
        TenantParameterValueType valueType,
        TenantParameterCategory category,
        bool isActive,
        bool isSensitive,
        string? defaultValue,
        string? allowedValues,
        AuditValueObject audit,
        bool isDeleted = false)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        Description = description;
        Value = value;
        ValueType = valueType;
        Category = category;
        IsActive = isActive;
        IsSensitive = isSensitive;
        DefaultValue = defaultValue;
        AllowedValues = allowedValues;
        Audit = audit;
        IsDeleted = isDeleted;
    }

    public IdValueObject Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public CodeEntity Code { get; private set; }
    public DescriptionEntity Description { get; private set; }
    public string Value { get; private set; }
    public TenantParameterValueType ValueType { get; private set; }
    public TenantParameterCategory Category { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsSensitive { get; private set; }
    public string? DefaultValue { get; private set; }
    public string? AllowedValues { get; private set; }
    public AuditValueObject Audit { get; private set; }

    /// <summary>
    /// Marca de borrado LÓGICO. Es un eje INDEPENDIENTE de <see cref="IsActive"/>: «inactivo» es un
    /// estado vivo y reversible —<c>Reactivate</c> lo devuelve al servicio— mientras que «eliminado»
    /// es terminal y desaparece de toda lectura. Reutilizar IsActive para el borrado permitiría
    /// resucitar un parámetro eliminado con una simple reactivación.
    /// </summary>
    public bool IsDeleted { get; private set; }

    public TenantParameterProps WithValue(string value)
    {
        var clone = (TenantParameterProps)MemberwiseClone();
        clone.Value = value;
        return clone;
    }

    public TenantParameterProps WithIsActive(bool isActive)
    {
        var clone = (TenantParameterProps)MemberwiseClone();
        clone.IsActive = isActive;
        return clone;
    }

    public TenantParameterProps WithIsDeleted(bool isDeleted)
    {
        var clone = (TenantParameterProps)MemberwiseClone();
        clone.IsDeleted = isDeleted;
        return clone;
    }

    public object Clone()
    {
        return MemberwiseClone();
    }
}