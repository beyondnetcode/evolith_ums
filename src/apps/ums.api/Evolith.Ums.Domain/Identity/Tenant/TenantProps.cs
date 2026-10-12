namespace Evolith.Ums.Domain.Identity.Tenant;

public class TenantProps : IProps
{
    public IdValueObject Id { get; private set; }
    public Code Code { get; private set; }
    public Name Name { get; private set; }
    public OrganizationType Type { get; private set; }
    public IdpStrategy IdpStrategy { get; private set; }
    public CompanyReference? CompanyReference { get; private set; }
    public TenantId? ParentTenantId { get; private set; }
    public bool IsManagementOwner { get; private set; }
    public TenantStatus Status { get; private set; }

    // FR-042 (ADR-UMS-097 §2.2): suite por defecto del inquilino. Es la procedencia de la suite
    // pre-autenticación cuando el AccessScope no la fija. Nullable/retrocompatible: un inquilino de
    // suite única la deja en null y la resolución omite el filtro por suite (comportamiento previo).
    public SystemSuiteId? DefaultSystemSuiteId { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public TenantProps(
        IdValueObject id,
        Code code,
        Name name,
        OrganizationType type,
        IdpStrategy idpStrategy,
        CompanyReference? companyReference,
        TenantId? parentTenantId,
        bool isManagementOwner,
        ActorId createdBy)
    {
        Id = id;
        Code = code;
        Name = name;
        Type = type;
        IdpStrategy = idpStrategy;
        CompanyReference = companyReference;
        ParentTenantId = parentTenantId;
        IsManagementOwner = isManagementOwner;
        Status = TenantStatus.Active;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public TenantProps(
        IdValueObject id,
        Code code,
        Name name,
        OrganizationType type,
        IdpStrategy idpStrategy,
        CompanyReference? companyReference,
        TenantId? parentTenantId,
        bool isManagementOwner,
        SystemSuiteId? defaultSystemSuiteId,
        TenantStatus status,
        AuditValueObject audit)
    {
        Id = id;
        Code = code;
        Name = name;
        Type = type;
        IdpStrategy = idpStrategy;
        CompanyReference = companyReference;
        ParentTenantId = parentTenantId;
        IsManagementOwner = isManagementOwner;
        DefaultSystemSuiteId = defaultSystemSuiteId;
        Status = status;
        Audit = audit;
    }

    public TenantProps WithManagementOwner(bool isManagementOwner)
    {
        var clone = (TenantProps)MemberwiseClone();
        clone.IsManagementOwner = isManagementOwner;
        return clone;
    }

    // FS-26 (G-024): actualización de los datos generales editables del tenant.
    // El Code es el identificador único y permanece inmutable.
    public TenantProps WithGeneralData(Name name, OrganizationType type, CompanyReference? companyReference)
    {
        var clone = (TenantProps)MemberwiseClone();
        clone.Name = name;
        clone.Type = type;
        clone.CompanyReference = companyReference;
        return clone;
    }

    public TenantProps WithStatus(TenantStatus status)
    {
        var clone = (TenantProps)MemberwiseClone();
        clone.Status = status;
        return clone;
    }

    // FR-042 (ADR-UMS-097 §2.2): fija/limpia la suite por defecto del inquilino.
    public TenantProps WithDefaultSystemSuite(SystemSuiteId? defaultSystemSuiteId)
    {
        var clone = (TenantProps)MemberwiseClone();
        clone.DefaultSystemSuiteId = defaultSystemSuiteId;
        return clone;
    }

    public object Clone()
    {
        return this.MemberwiseClone();
    }
}
