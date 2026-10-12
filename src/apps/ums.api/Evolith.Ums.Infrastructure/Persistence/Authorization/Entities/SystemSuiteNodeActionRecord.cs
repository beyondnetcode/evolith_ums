namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

/// <summary>
/// Puente N:M entre un nodo hoja (Opción) y una funcionalidad del catálogo
/// (`SystemSuiteActions.Code`) — ADR-0090. Una opción puede vincular varias
/// funcionalidades y una funcionalidad puede estar en varias opciones.
/// </summary>
public sealed class SystemSuiteNodeActionRecord
{
    public Guid Id { get; set; }
    public Guid NodeId { get; set; }
    public string ActionCode { get; set; } = string.Empty;

    public SystemSuiteNodeRecord Node { get; set; } = null!;
}
