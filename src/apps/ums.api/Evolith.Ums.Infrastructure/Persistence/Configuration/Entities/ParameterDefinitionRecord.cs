using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;

public sealed class ParameterDefinitionRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DataTypeId { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
    public int ScopeId { get; set; }
    public bool IsActive { get; set; }
    public bool IsMandatory { get; set; }
    public int DisplayOrder { get; set; }

    // Soft-delete (política del propietario: nunca se borra la fila). Mismos tres campos que
    // UserAccountRecord (REC-16) para que el patrón sea uno solo en todo el repositorio.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    public string Version { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
}