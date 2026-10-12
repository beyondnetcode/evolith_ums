using System.Data;
using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant.Branch;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;

namespace Evolith.Ums.Infrastructure.Persistence.Identity;

using TenantAggregate = Evolith.Ums.Domain.Identity.Tenant.Tenant;

public sealed class PostgreSqlTenantRepository(UmsPlatformDbContext dbContext) : ITenantRepository, IUnitOfWork
{
    private readonly HashSet<TenantAggregate> _trackedAggregates = [];

    public IUnitOfWork UnitOfWork => this;

    public async Task<TenantAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.Tenants
            .AsSingleQuery()
            .Include(x => x.Branches)
            .Include(x => x.IdentityProviders)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public Task<TenantAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    // G-161: verificación de unicidad autoritativa que IGNORA el filtro global por inquilino. Necesaria
    // en la vía de escritura porque el filtro acota la colección `Branches` del agregado al inquilino
    // del CONTEXTO; un admin interno que provisiona sobre otro inquilino cargaría una colección vacía y
    // la guarda en memoria no vería el duplicado. La lectura sigue aislada (esta consulta no expone
    // datos: solo devuelve un booleano y se usa dentro de un handler ya acotado a management-owner).
    //
    // ADR-0164 §2.3: tampoco filtra por `IsClosed`. El código de una sucursal cerrada sigue ocupado,
    // y esta consulta es la que hace que el alta lo diga con un conflicto legible en vez de dejar
    // que reviente el índice único con un 23505.
    public Task<bool> BranchCodeExistsAsync(Guid tenantId, string code, CancellationToken cancellationToken = default)
        => dbContext.TenantBranches
            .IgnoreQueryFilters()
            .AnyAsync(b => b.TenantId == tenantId && b.Code == code, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BranchLifecycleEntry>> GetBranchLifecycleAsync(
        Guid tenantId, Guid branchId, CancellationToken cancellationToken = default)
    {
        var filas = await dbContext.TenantBranchLifecycleEntries
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BranchId == branchId)
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.EpisodeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return filas.Select(f => new BranchLifecycleEntry(
            f.Id,
            f.TenantId,
            f.BranchId,
            DomainEnumerationMapper.FromValue<BranchLifecycleEpisode>(f.EpisodeId),
            f.OccurredAtUtc,
            f.ActorId,
            f.NameSnapshot,
            f.GeofencingSnapshot,
            f.Reason)).ToList();
    }

    public async Task<TenantAggregate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.Tenants
            .Include(x => x.Branches)
            .Include(x => x.IdentityProviders)
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<IReadOnlyList<TenantAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var baseQuery = dbContext.Tenants.AsQueryable();

        if (tenantId.HasValue)
        {
            // Scope to the tenant itself and any direct child tenants
            baseQuery = baseQuery.Where(t => t.Id == tenantId.Value || t.ParentTenantId == tenantId.Value);
        }

        var records = await baseQuery
            .AsSplitQuery()
            .Include(x => x.Branches)
            .Include(x => x.IdentityProviders)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<TenantAggregate> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, string? status, string sortBy, string sortOrder,
        Guid? tenantId = null, CancellationToken cancellationToken = default, string? searchField = null)
    {
        // REC-12: Apply all filtering at the DB level before Skip/Take to avoid loading full tables.
        var query = dbContext.Tenants.AsQueryable();

        // Tenant isolation: scope to the tenant itself and its direct children
        if (tenantId.HasValue)
        {
            query = query.Where(t => t.Id == tenantId.Value || t.ParentTenantId == tenantId.Value);
        }

        // --- Filtering ---
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            // El campo de búsqueda lo determina `searchField` (parámetro `criteria` del API), no el
            // orden: buscar por código no debe exigir ordenar por código. Fallback a sortBy si viene vacío.
            var field = string.IsNullOrWhiteSpace(searchField) ? sortBy : searchField;
            query = (field.ToLower()) switch
            {
                "code" => query.Where(t => t.Code.ToLower().Contains(lower)),
                _      => query.Where(t => t.Name.ToLower().Contains(lower)),
            };
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            // StatusId is stored as int; compare by name via enum lookup if mapping exists
            // For now filter by string representation at application level after fetch
            // (StatusId-to-name mapping is in the domain enum).
            // We do the simple case: filter after fetching the paged batch (small set).
        }

        // --- Total count before pagination ---
        var totalCount = await query.CountAsync(cancellationToken);

        // --- Sorting ---
        query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
        {
            ("code", "desc") => query.OrderByDescending(t => t.Code),
            ("code", _)      => query.OrderBy(t => t.Code),
            ("name", "desc") => query.OrderByDescending(t => t.Name),
            _                => query.OrderBy(t => t.Name),
        };

        // --- Pagination (DB-level Skip/Take) ---
        var records = await query
            .AsSplitQuery()
            .Include(x => x.Branches)
            .Include(x => x.IdentityProviders)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (records.Select(Rehydrate).ToList(), totalCount);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Uses EF change tracking so soft-delete changes are committed together with the
    /// TenantDeletedEvent outbox message in a single SaveChangesAsync call by the caller.
    /// The caller MUST invoke UpdateAsync first (to populate _trackedAggregates) and then
    /// call this method before SaveEntitiesAsync.
    /// </remarks>
    public async Task<bool> SoftDeleteAsync(Guid id, string deletedBy, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (record is null) return false;

        var now = DateTime.UtcNow;
        record.IsDeleted = true;
        record.DeletedAtUtc = now;
        record.DeletedBy = deletedBy;
        return true;
    }

    public async Task AddAsync(TenantAggregate aggregate, CancellationToken cancellationToken = default)
    {
        dbContext.Tenants.Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        await Task.CompletedTask;
    }

    public async Task UpdateAsync(TenantAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Tenants
            .Include(x => x.Branches)
            .Include(x => x.IdentityProviders)
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {aggregate.Props.Id.GetValue()} does not exist.");

        Apply(existing, aggregate);
        _trackedAggregates.Add(aggregate);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        // Capture outbox messages BEFORE committing so events are not lost on save failure.
        // MarkChangesAsCommitted() is called AFTER SaveChangesAsync succeeds (FIX-01).
        // Publicación pre-commit = bug (mensaje fantasma); se stagea vía outbox y se entrega
        // post-commit. Patrón e interpretación: KB-TXN-001 (evolith-core); ADR-0098 D4.1 / G-066.
        foreach (var aggregate in _trackedAggregates)
        {
            await dbContext.PublishDomainEventsAsync(aggregate.DomainEvents.GetUncommittedChanges(), cancellationToken);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            // FIX-03: surface optimistic concurrency failures as 409 Conflict
            var entry = ex.Entries.FirstOrDefault();
            var id = (Guid)(entry?.Property("Id").CurrentValue ?? Guid.Empty);
            throw new ConcurrencyConflictException(entry?.Metadata.Name ?? "Unknown", id);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            when (ErroresDePersistencia.EsViolacionDeUnicidad(ex.InnerException))
        {
            // G-045/G-037: red de seguridad. Si una violación de índice único (23505) escapa a las
            // guardas de aplicación (p.ej. IX_Tenants_SingleManagementOwner ante una carrera),
            // la traducimos a 409 Conflict en vez de dejar que caiga a 500.
            var entry = ex.Entries.FirstOrDefault();
            var id = (Guid)(entry?.Property("Id").CurrentValue ?? Guid.Empty);
            throw new ConcurrencyConflictException(entry?.Metadata.Name ?? "Unknown", id);
        }

        foreach (var aggregate in _trackedAggregates)
        {
            aggregate.DomainEvents.MarkChangesAsCommitted();
            // Los asientos de bitácora ya están en la base: vaciar el búfer evita que un segundo
            // guardado del mismo agregado vuelva a intentar insertarlos (ADR-0164).
            aggregate.MarkBranchLifecycleAsCommitted();
        }

        _trackedAggregates.Clear();
        return true;
    }

    public void Dispose() => dbContext.Dispose();

    private static TenantAggregate Rehydrate(TenantRecord record)
        => IdentityAggregateFactory.RehydrateTenant(record, record.Branches, record.IdentityProviders);

    private static TenantRecord ToRecord(TenantAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();

        return new TenantRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            Code = aggregate.Code.GetValue(),
            Name = aggregate.Name.GetValue(),
            OrganizationTypeId = aggregate.Type.Id,
            IdpStrategyId = aggregate.IdpStrategy.Id,
            CompanyReference = aggregate.CompanyReference?.GetValue(),
            ParentTenantId = aggregate.ParentTenantId?.GetValue(),
            IsManagementOwner = aggregate.IsManagementOwner,
            // FR-042 (ADR-UMS-097 §2.2): suite por defecto del inquilino (nullable/retrocompatible).
            DefaultSystemSuiteId = aggregate.DefaultSystemSuiteId?.GetValue(),
            StatusId = aggregate.Status.Id,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
            Branches = aggregate.Branches.Select(branch =>
            {
                var a = branch.Props.Audit.GetValue();
                return new TenantBranchRecord
                {
                    Id = branch.Props.Id.GetValue(),
                    TenantId = branch.Props.TenantId.GetValue(),
                    Code = branch.Code.GetValue(),
                    Name = branch.Name.GetValue(),
                    GeofencingMetadata = branch.GeofencingMetadata?.GetValue(),
                    IsActive = branch.IsActive,
                    IsClosed = branch.IsClosed,
                    ClosedAtUtc = branch.ClosedAtUtc,
                    ClosedBy = branch.ClosedBy,
                    CreatedBy = a.CreatedBy,
                    CreatedAtUtc = a.CreatedAt,
                    UpdatedBy = a.UpdatedBy,
                    UpdatedAtUtc = a.UpdatedAt,
                    AuditTimeSpan = a.TimeSpan,
                    // Solo los asientos de ESTA unidad de trabajo: la bitácora histórica no se carga
                    // con el agregado, así que aquí no hay nada que reescribir, solo que añadir.
                    LifecycleEntries = branch.PendingLifecycleEntries.Select(ToLifecycleRecord).ToList(),
                };
            }).ToList(),
            IdentityProviders = aggregate.IdentityProviders.Select(provider =>
            {
                var a = provider.Props.Audit.GetValue();
                return new TenantIdentityProviderRecord
                {
                    Id = provider.Props.Id.GetValue(),
                    TenantId = provider.Props.TenantId.GetValue(),
                    Code = provider.Code.GetValue(),
                    Name = provider.Name.GetValue(),
                    Description = provider.Description.GetValue(),
                    StrategyId = provider.Strategy.Id,
                    IsActive = provider.IsActive,
                    CreatedBy = a.CreatedBy,
                    CreatedAtUtc = a.CreatedAt,
                    UpdatedBy = a.UpdatedBy,
                    UpdatedAtUtc = a.UpdatedAt,
                    AuditTimeSpan = a.TimeSpan,
                };
            }).ToList(),
        };
    }

    private void Apply(TenantRecord target, TenantAggregate source)
    {
        var replacement = ToRecord(source);

        target.Code = replacement.Code;
        target.Name = replacement.Name;
        target.OrganizationTypeId = replacement.OrganizationTypeId;
        target.IdpStrategyId = replacement.IdpStrategyId;
        target.CompanyReference = replacement.CompanyReference;
        target.ParentTenantId = replacement.ParentTenantId;
        target.IsManagementOwner = replacement.IsManagementOwner;
        target.DefaultSystemSuiteId = replacement.DefaultSystemSuiteId;
        target.StatusId = replacement.StatusId;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;

        UpsertBranches(target.Branches, replacement.Branches);
        UpsertIdentityProviders(target.IdentityProviders, replacement.IdentityProviders);

    }

    private void UpsertBranches(ICollection<TenantBranchRecord> target, IEnumerable<TenantBranchRecord> source)
    {
        var reemplazo = source.ToList();

        EfChildCollectionReconciler.ReconcileById(
            dbContext,
            target,
            reemplazo,
            branch => branch.Id,
            UpdateBranch);

        AppendLifecycleEntries(target, reemplazo);
    }

    /// <summary>
    /// Vuelca los asientos de bitácora del agregado en la MISMA transacción que el cambio de estado.
    ///
    /// <para>Va aquí y no en un manejador de eventos post-commit porque ese despacho es, por
    /// contrato (ADR-0098 D4), best-effort: un fallo se registra como advertencia y no revierte
    /// nada. Una bitácora que puede perder episodios en silencio no sirve como prueba de auditoría.</para>
    ///
    /// <para>Solo AÑADE. No actualiza ni borra, y por eso no usa el reconciliador: un asiento es
    /// inmutable, y la colección rastreada no está cargada —la bitácora no viaja con el agregado—,
    /// así que reconciliar por ausencia habría intentado borrar toda la historia.</para>
    /// </summary>
    private void AppendLifecycleEntries(ICollection<TenantBranchRecord> target, IEnumerable<TenantBranchRecord> source)
    {
        var porId = target.ToDictionary(b => b.Id);

        foreach (var origen in source)
        {
            if (origen.LifecycleEntries.Count == 0) continue;
            if (!porId.TryGetValue(origen.Id, out var destino)) continue;

            // Cuando la sucursal es NUEVA, el reconciliador ya insertó en `target` el mismo objeto
            // que trae los asientos: no hay nada que trasvasar, solo que marcarlos como altas.
            var esLaMismaInstancia = ReferenceEquals(destino, origen);
            var yaPresentes = esLaMismaInstancia
                ? []
                : destino.LifecycleEntries.Select(e => e.Id).ToHashSet();

            foreach (var asiento in origen.LifecycleEntries.ToList())
            {
                if (!esLaMismaInstancia)
                {
                    if (!yaPresentes.Add(asiento.Id)) continue;

                    asiento.BranchId = destino.Id;
                    destino.LifecycleEntries.Add(asiento);
                }

                // Se marca explícitamente en vez de confiar en que la detección de cambios descubra
                // los hijos nuevos a través de la navegación: el asiento es la evidencia de
                // auditoría, y no puede depender de una inferencia.
                dbContext.Entry(asiento).State = EntityState.Added;
            }
        }
    }

    private static TenantBranchLifecycleEntryRecord ToLifecycleRecord(BranchLifecycleEntry entry)
        => new()
        {
            Id = entry.Id,
            TenantId = entry.TenantId,
            BranchId = entry.BranchId,
            EpisodeId = entry.Episode.Id,
            OccurredAtUtc = entry.OccurredAtUtc,
            ActorId = entry.ActorId,
            NameSnapshot = entry.NameSnapshot,
            GeofencingSnapshot = entry.GeofencingSnapshot,
            Reason = entry.Reason,
        };

    private void UpsertIdentityProviders(ICollection<TenantIdentityProviderRecord> target, IEnumerable<TenantIdentityProviderRecord> source)
    {
        EfChildCollectionReconciler.ReconcileById(
            dbContext,
            target,
            source,
            provider => provider.Id,
            UpdateIdentityProvider);
    }

    private static void UpdateBranch(TenantBranchRecord target, TenantBranchRecord source)
    {
        target.TenantId = source.TenantId;
        target.Code = source.Code;
        target.Name = source.Name;
        target.GeofencingMetadata = source.GeofencingMetadata;
        target.IsActive = source.IsActive;
        target.IsClosed = source.IsClosed;
        target.ClosedAtUtc = source.ClosedAtUtc;
        target.ClosedBy = source.ClosedBy;
        target.CreatedBy = source.CreatedBy;
        target.CreatedAtUtc = source.CreatedAtUtc;
        target.UpdatedBy = source.UpdatedBy;
        target.UpdatedAtUtc = source.UpdatedAtUtc;
        target.AuditTimeSpan = source.AuditTimeSpan;
    }

    private static void UpdateIdentityProvider(TenantIdentityProviderRecord target, TenantIdentityProviderRecord source)
    {
        target.TenantId = source.TenantId;
        target.Code = source.Code;
        target.Name = source.Name;
        target.Description = source.Description;
        target.StrategyId = source.StrategyId;
        target.IsActive = source.IsActive;
        target.CreatedBy = source.CreatedBy;
        target.CreatedAtUtc = source.CreatedAtUtc;
        target.UpdatedBy = source.UpdatedBy;
        target.UpdatedAtUtc = source.UpdatedAtUtc;
        target.AuditTimeSpan = source.AuditTimeSpan;
    }
}
