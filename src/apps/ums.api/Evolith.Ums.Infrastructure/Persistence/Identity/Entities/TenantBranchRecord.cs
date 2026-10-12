using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

public sealed class TenantBranchRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? GeofencingMetadata { get; set; }
    public bool IsActive { get; set; }

    // ADR-0164 §2.1: cierre definitivo. La fila permanece; lo que cambia es que deja de listarse y
    // deja de admitir transiciones. `IsActive` sigue siendo el eje reversible y no se toca.
    public bool IsClosed { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedBy { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;

    public TenantRecord Tenant { get; set; } = default!;

    /// <summary>Bitácora de episodios de la sucursal. Solo crece: nunca se actualiza ni se borra.</summary>
    public ICollection<TenantBranchLifecycleEntryRecord> LifecycleEntries { get; set; } = new List<TenantBranchLifecycleEntryRecord>();
}
