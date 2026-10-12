namespace Evolith.Ums.Domain.Identity.Tenant.Branch;

public sealed class Branch : Entity<Branch, BranchProps>
{
    /// <summary>
    /// Episodios registrados en ESTA unidad de trabajo y todavía sin persistir.
    ///
    /// La bitácora histórica NO se carga con el agregado a propósito: ninguna invariante de
    /// <c>Tenant</c> depende de ella, y arrastrarla en cada lectura del inquilino —que es una ruta
    /// caliente, la usa hasta el grafo de autorización— costaría filas que nadie mira. Lo que sí es
    /// obligatorio es que el asiento se escriba en la MISMA transacción que el cambio de estado: un
    /// manejador post-commit es, por contrato (ADR-0098 D4), best-effort, y una bitácora que puede
    /// perder episodios no prueba nada. De ahí este búfer: el dominio decide y anota, el repositorio
    /// vuelca en el mismo <c>SaveChanges</c>.
    /// </summary>
    private readonly List<BranchLifecycleEntry> _pendingLifecycleEntries = [];

    private Branch(BranchProps props) : base(props)
    {
    }

    public TenantId TenantId => Props.TenantId;
    public Code Code => Props.Code;
    public Name Name => Props.Name;
    public Value? GeofencingMetadata => Props.GeofencingMetadata;
    public bool IsActive => Props.IsActive;

    /// <summary>
    /// Verdadero cuando la sucursal está CERRADA DEFINITIVAMENTE (ADR-0164 §2.1). La fila sigue en
    /// la base —y su código sigue ocupado— pero la sucursal ya no existe para el negocio.
    /// </summary>
    public bool IsClosed => Props.IsClosed;
    public DateTime? ClosedAtUtc => Props.ClosedAtUtc;
    public string? ClosedBy => Props.ClosedBy;

    public IReadOnlyCollection<BranchLifecycleEntry> PendingLifecycleEntries => _pendingLifecycleEntries.AsReadOnly();

    public BranchId GetId() => BranchId.Load(Props.Id.GetValue());

    public static Result<Branch> Create(TenantId tenantId, Code code, Name name, ActorId createdBy, Value? geofencingMetadata = null)
    {
        var props = new BranchProps(IdValueObject.Create(), tenantId, code, name, geofencingMetadata, createdBy);
        var branch = new Branch(props);

        if (!branch.IsValid())
        {
            return Result<Branch>.Failure(branch.BrokenRules.GetBrokenRulesAsString());
        }

        // La apertura es el episodio cero: sin él la bitácora empezaría a media historia y no se
        // podría situar el primer despacho de la sucursal en ninguna época.
        branch.RecordEpisode(BranchLifecycleEpisode.Opened, createdBy, reason: null);

        return Result<Branch>.Success(branch);
    }

    internal Result CanDeactivate()
    {
        if (IsClosed)
        {
            // Una sucursal cerrada no admite NINGUNA transición: su ciclo de vida terminó.
            BrokenRules.Add(new BrokenRule(nameof(IsClosed), DomainErrors.Tenant.BranchClosed));
        }
        else if (!IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Common.Invalid));
        }

        return IsValid()
            ? Result.Success()
            : Result.Failure(BrokenRules.GetBrokenRulesAsString());
    }

    internal Result CanReactivate()
    {
        if (IsClosed)
        {
            // El cierre es TERMINAL: reactivar no puede ser la puerta de atrás que resucite una
            // sucursal cerrada (ADR-0164 §2.4). Si el negocio vuelve a abrir en esa plaza, se da de
            // alta una sucursal NUEVA —con otro código, porque el anterior queda ocupado— y la
            // cerrada permanece como histórico consultable.
            BrokenRules.Add(new BrokenRule(nameof(IsClosed), DomainErrors.Tenant.BranchClosed));
        }
        else if (IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Common.Invalid));
        }

        return IsValid()
            ? Result.Success()
            : Result.Failure(BrokenRules.GetBrokenRulesAsString());
    }

    /// <summary>
    /// ¿Puede cerrarse definitivamente? Solo comprueba lo que el propio agregado sabe; las
    /// referencias vivas de FUERA del agregado (usuarios y perfiles activos) las verifica
    /// <c>Tenant.CloseBranch</c> con los recuentos que le pasa la aplicación.
    /// </summary>
    internal Result CanClose()
    {
        if (IsClosed)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsClosed), DomainErrors.Tenant.BranchAlreadyClosed));
        }

        return IsValid()
            ? Result.Success()
            : Result.Failure(BrokenRules.GetBrokenRulesAsString());
    }

    // FS-26 (G-024): actualiza los datos editables de la sucursal (nombre, geocerca).
    // Invocado por el agregado raíz Tenant (Tenant.UpdateBranch).
    internal void UpdateInternal(Name name, Value? geofencingMetadata)
    {
        Props.Update(name, geofencingMetadata);
    }

    internal void DeactivateInternal(ActorId actor, string? reason)
    {
        Props.IsActive = false;
        Props.Audit.Update(actor.GetValue());
        RecordEpisode(BranchLifecycleEpisode.Deactivated, actor, reason);
    }

    internal void ReactivateInternal(ActorId actor, string? reason)
    {
        Props.IsActive = true;
        Props.Audit.Update(actor.GetValue());
        RecordEpisode(BranchLifecycleEpisode.Reactivated, actor, reason);
    }

    /// <summary>
    /// Cierre DEFINITIVO. Marca el estado terminal y apaga <c>IsActive</c> como CONSECUENCIA —una
    /// sucursal cerrada evidentemente no opera—, nunca como mecanismo: lo que impide la vuelta es
    /// <see cref="IsClosed"/>, y por eso <see cref="CanReactivate"/> lo mira a él.
    /// </summary>
    internal void CloseInternal(ActorId closedBy, string? reason)
    {
        var now = DateTime.UtcNow;

        Props.IsClosed = true;
        Props.ClosedAtUtc = now;
        Props.ClosedBy = closedBy.GetValue();
        Props.IsActive = false;
        Props.Audit.Update(closedBy.GetValue());

        RecordEpisode(BranchLifecycleEpisode.Closed, closedBy, reason, now);
    }

    /// <summary>
    /// El repositorio llama a esto tras confirmar la transacción, igual que
    /// <c>DomainEvents.MarkChangesAsCommitted()</c>: los asientos ya están en la base y el búfer no
    /// debe volver a volcarlos.
    /// </summary>
    internal void MarkLifecycleEntriesAsCommitted() => _pendingLifecycleEntries.Clear();

    private void RecordEpisode(BranchLifecycleEpisode episode, ActorId actor, string? reason, DateTime? occurredAtUtc = null)
    {
        _pendingLifecycleEntries.Add(new BranchLifecycleEntry(
            Id: Guid.NewGuid(),
            TenantId: Props.TenantId.GetValue(),
            BranchId: Props.Id.GetValue(),
            Episode: episode,
            OccurredAtUtc: occurredAtUtc ?? DateTime.UtcNow,
            ActorId: actor.GetValue(),
            // La foto se toma DESPUÉS de aplicar el cambio de estado, así que un asiento de cierre
            // guarda el nombre y la geocerca con los que la sucursal llegó a su último día.
            NameSnapshot: Props.Name.GetValue(),
            GeofencingSnapshot: Props.GeofencingMetadata?.GetValue(),
            Reason: reason));
    }
}
