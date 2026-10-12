using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

public sealed class TenantParameterRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int ValueTypeId { get; set; }
    public int CategoryId { get; set; }
    public bool IsActive { get; set; }
    /// <summary>
    /// Borrado LÓGICO (política del propietario: el borrado físico no se permite). Independiente de
    /// <see cref="IsActive"/>: inactivo es reversible, eliminado es terminal.
    /// </summary>
    public bool IsDeleted { get; set; }
    public bool IsSensitive { get; set; }
    public string? DefaultValue { get; set; }
    public string? AllowedValues { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
}