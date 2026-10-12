using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

public sealed class TenantRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int OrganizationTypeId { get; set; }
    public int IdpStrategyId { get; set; }
    public string? CompanyReference { get; set; }
    public Guid? ParentTenantId { get; set; }
    public bool IsManagementOwner { get; set; }

    // FR-042 (ADR-UMS-097 §2.2): suite por defecto del inquilino. Columna nullable/retrocompatible:
    // los inquilinos existentes quedan NULL y la resolución de IdP omite el filtro por suite.
    public Guid? DefaultSystemSuiteId { get; set; }
    public int StatusId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];

    // REC-16: Soft-delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    public List<TenantBranchRecord> Branches { get; set; } = [];
    public List<TenantIdentityProviderRecord> IdentityProviders { get; set; } = [];
}
