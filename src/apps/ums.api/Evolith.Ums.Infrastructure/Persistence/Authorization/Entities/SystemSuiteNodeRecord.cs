namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

/// <summary>
/// Nodo del árbol de navegación recursivo (ADR-0090). Adjacency list:
/// <see cref="ParentNodeId"/> nulo ⇒ hijo directo del módulo. Reemplazará a
/// SystemSuiteMenus/SubMenus/Options (convivencia aditiva durante la migración).
/// </summary>
public sealed class SystemSuiteNodeRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid ModuleId { get; set; }
    public Guid? ParentNodeId { get; set; }
    public int NodeKindId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Presentación (G-178): identificador de icono y ruta destino. Opcionales.</summary>
    public string? Icon { get; set; }
    public string? Route { get; set; }

    // ── Metadatos de gobernanza SDLC (opcionales) ──
    public string? Responsable { get; set; }
    public string? Criticidad { get; set; }
    public string? ProductoImpactado { get; set; }
    public string? ComponenteTecnico { get; set; }
    public string? Dependencias { get; set; }
    public string? Evidencias { get; set; }
    public string? TrazabilidadSdlc { get; set; }

    // ── Auditoría ──
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;

    // ── Navegaciones ──
    public SystemSuiteModuleRecord Module { get; set; } = null!;
    public SystemSuiteNodeRecord? Parent { get; set; }
    public List<SystemSuiteNodeRecord> Children { get; set; } = [];
    public List<SystemSuiteNodeActionRecord> Actions { get; set; } = [];
}
