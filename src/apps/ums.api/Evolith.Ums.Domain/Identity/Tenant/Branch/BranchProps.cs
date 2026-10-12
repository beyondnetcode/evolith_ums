namespace Evolith.Ums.Domain.Identity.Tenant.Branch;

public class BranchProps : IProps
{
    public IdValueObject Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Code Code { get; private set; }
    public Name Name { get; private set; }
    public Value? GeofencingMetadata { get; private set; }
    public bool IsActive { get; set; }
    public AuditValueObject Audit { get; private set; }

    /// <summary>
    /// Marca de CIERRE DEFINITIVO (ADR-0164 §2.1). Es un eje INDEPENDIENTE de <see cref="IsActive"/>
    /// y no una reutilización suya: «inactiva» es un estado vivo y reversible —<c>Reactivate</c> la
    /// devuelve al servicio— mientras que «cerrada» es terminal y no admite vuelta. Colgar el cierre
    /// de <see cref="IsActive"/> permitiría resucitar una sucursal cerrada con una simple
    /// reactivación, que es justo lo que ADR-0164 §2.4 prohíbe.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>Instante del cierre definitivo, en UTC. Nulo mientras la sucursal siga existiendo.</summary>
    public DateTime? ClosedAtUtc { get; set; }

    /// <summary>Actor que cerró la sucursal. Nulo mientras no se haya cerrado.</summary>
    public string? ClosedBy { get; set; }

    public BranchProps(IdValueObject id, TenantId tenantId, Code code, Name name, Value? geofencingMetadata, ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        Name = name;
        GeofencingMetadata = geofencingMetadata;
        IsActive = true;
        IsClosed = false;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    // FS-26 (G-024): datos editables de la sucursal. El Code es único dentro del tenant y no cambia.
    public void Update(Name name, Value? geofencingMetadata)
    {
        Name = name;
        GeofencingMetadata = geofencingMetadata;
    }

    public object Clone()
    {
        return this.MemberwiseClone();
    }
}
