namespace Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

/// <summary>
/// Fila de la BITÁCORA de una sucursal (ADR-0164). Append-only por diseño: no lleva columnas de
/// auditoría de modificación porque un asiento no se modifica —el instante y el autor del episodio
/// SON el dato—, y una bitácora que se puede reescribir no prueba nada.
/// </summary>
public sealed class TenantBranchLifecycleEntryRecord
{
    public Guid Id { get; set; }

    /// <summary>Inquilino dueño de la sucursal. Sostiene el filtro global y la política RLS.</summary>
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    /// <summary>Identificador de <c>BranchLifecycleEpisode</c>. Estable: se persiste.</summary>
    public int EpisodeId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string ActorId { get; set; } = string.Empty;

    /// <summary>Nombre de la sucursal en el instante del episodio (foto de la época).</summary>
    public string NameSnapshot { get; set; } = string.Empty;

    /// <summary>Geocerca en el instante del episodio; nula si la sucursal no tenía.</summary>
    public string? GeofencingSnapshot { get; set; }

    public string? Reason { get; set; }

    public TenantBranchRecord Branch { get; set; } = default!;
}
