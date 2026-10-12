namespace Evolith.Ums.Contracts.MasterData;

/// <summary>
/// Contrato de mensajería (wire) para eventos de Tenant maestro publicados por MMS.
/// El nombre canónico del contrato se fija en <c>Evolith.Ums.Contracts.MasterData</c>.
/// Decisión D-006 / G-011: hoy NO existe un productor MMS real (prototipo), por lo que
/// este consumidor está inerte por ausencia de mensajes — no hay riesgo latente. Cuando
/// exista integración real, el productor debe publicar con este mismo namespace+tipo
/// (MassTransit enruta por el URN del mensaje); no cambiar nombres de campo ni namespace
/// de forma unilateral respecto del productor.
/// </summary>
public sealed record TenantEvent
{
    public string SpecVersion { get; init; } = "1.0";
    public Guid Id { get; init; }
    public string Type { get; init; } = default!;
    public string Source { get; init; } = "mms";
    public Guid Subject { get; init; }
    public DateTimeOffset Time { get; init; }
    public string SchemaVersion { get; init; } = "1.0";
    public long Sequence { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }
    public string Actor { get; init; } = "system";
    public DateTimeOffset ProducedAt { get; init; }
    public TenantData Data { get; init; } = default!;
}

public sealed record TenantData(Guid TenantId, string Code, string Name, string Status, long Version);
