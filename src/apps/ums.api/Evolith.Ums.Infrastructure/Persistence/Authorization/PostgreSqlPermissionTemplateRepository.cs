using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization;

using PermissionTemplateAggregate = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate;

public sealed class PostgreSqlPermissionTemplateRepository(UmsPlatformDbContext dbContext) : IPermissionTemplateRepository, IUnitOfWork
{
    private readonly HashSet<PermissionTemplateAggregate> _trackedAggregates = [];

    /// <summary>
    /// Id del estado terminal de borrado lógico (<c>TemplateStatus.Deleted</c>). Se materializa como
    /// constante entera porque el filtro tiene que traducirse a SQL: EF no sabe evaluar la comparación
    /// de un DomainEnumeration del dominio dentro de una expresión LINQ.
    /// </summary>
    private const int DeletedStatusId = 4;

    public IUnitOfWork UnitOfWork => this;

    public async Task<PermissionTemplateAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.PermissionTemplates
            .AsSplitQuery()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.StatusId != DeletedStatusId, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<PermissionTemplateAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.PermissionTemplates
            .AsSplitQuery()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && x.StatusId != DeletedStatusId, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<IReadOnlyList<PermissionTemplateAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<PermissionTemplateRecord> query = dbContext.PermissionTemplates
            .AsSplitQuery()
            .Include(x => x.Items)
            .Where(x => x.StatusId != DeletedStatusId);

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var records = await query.OrderBy(x => x.RoleId).ThenBy(x => x.Version).ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<PermissionTemplateAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.PermissionTemplates
            .AsSplitQuery()
            .Include(x => x.Items)
            .Where(x => x.TenantId == tenantId && x.StatusId != DeletedStatusId)
            .OrderBy(x => x.RoleId)
            .ThenBy(x => x.Version)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    /// <summary>
    /// ÚNICA lectura que SÍ ve las plantillas lógicamente eliminadas, y es deliberado: su único
    /// consumidor es <c>CreatePermissionTemplateCommandHandler</c> para calcular la versión siguiente
    /// (<c>CreateNextVersion</c>, G-140). Una fila eliminada lógicamente SIGUE OCUPANDO su versión en
    /// IX_PermissionTemplates_TenantId_RoleId_SystemSuiteId_Version, que es un índice único sobre toda
    /// la tabla. Si la filtrásemos, el alta siguiente reutilizaría la versión de una eliminada y
    /// chocaría con 23505 → 409 opaco.
    /// </summary>
    public async Task<IReadOnlyList<PermissionTemplateAggregate>> GetByTenantRoleSuiteAsync(Guid tenantId, Guid roleId, Guid systemSuiteId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.PermissionTemplates
            .AsSplitQuery()
            .Include(x => x.Items)
            .Where(x => x.TenantId == tenantId && x.RoleId == roleId && x.SystemSuiteId == systemSuiteId)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public Task AddAsync(PermissionTemplateAggregate aggregate, CancellationToken cancellationToken = default)
    {
        dbContext.PermissionTemplates.Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(PermissionTemplateAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.PermissionTemplates
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"Permission template {aggregate.Props.Id.GetValue()} does not exist.");

        Apply(existing, aggregate);
        _trackedAggregates.Add(aggregate);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var aggregate in _trackedAggregates)
        {
            await dbContext.PublishDomainEventsAsync(aggregate.DomainEvents.GetUncommittedChanges(), cancellationToken);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.FirstOrDefault();
            var id = (Guid)(entry?.Property("Id").CurrentValue ?? Guid.Empty);
            throw new ConcurrencyConflictException(entry?.Metadata.Name ?? "Unknown", id);
        }
        catch (DbUpdateException ex)
            when (ErroresDePersistencia.EsViolacionDeUnicidad(ex.InnerException))
        {
            // G-140: red de seguridad. El alta ya resuelve la versión siguiente en el handler, pero
            // ante una carrera dos altas concurrentes sobre la misma terna pueden calcular la misma
            // versión y violar IX_PermissionTemplates_TenantId_RoleId_SystemSuiteId_Version (23505).
            // Se traduce a 409 Conflict en vez de dejar que caiga a 500 opaco (cf. PostgreSqlTenantRepository).
            var entry = ex.Entries.FirstOrDefault();
            var id = (Guid)(entry?.Property("Id").CurrentValue ?? Guid.Empty);
            throw new ConcurrencyConflictException(entry?.Metadata.Name ?? "Unknown", id);
        }

        foreach (var aggregate in _trackedAggregates)
        {
            aggregate.DomainEvents.MarkChangesAsCommitted();
        }

        _trackedAggregates.Clear();
        return true;
    }

    /// <summary>
    /// Borrado LÓGICO. Marca la fila con el estado terminal Deleted; NUNCA la quita de la tabla.
    ///
    /// Antes hacía <c>dbContext.PermissionTemplates.Remove(record)</c>, que además arrastraba por
    /// cascada todos los <c>PermissionTemplateItems</c>: se perdía sin remedio el rastro de qué
    /// concesiones había otorgado la plantilla, justo lo que el negocio consulta hacia atrás.
    ///
    /// Devuelve <c>false</c> si la fila no existe o si YA estaba eliminada, para que el handler
    /// distinga la carrera de dos borrados concurrentes (mismo contrato booleano que antes).
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.PermissionTemplates
            .FirstOrDefaultAsync(x => x.Id == id && x.StatusId != DeletedStatusId, cancellationToken);

        if (record is null) return false;

        record.StatusId = DeletedStatusId;
        return true;
    }

    // ── Dependency guard queries ────────────────────────────────────────────

    public Task<int> CountPublishedByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        => dbContext.PermissionTemplates.CountAsync(
            t => t.RoleId == roleId && t.StatusId == 2 /* Published */,
            cancellationToken);

    /// <summary>
    /// Los ítems de una plantilla lógicamente eliminada NO bloquean: su plantilla contenedora ya está
    /// eliminada, así que la referencia al recurso de dominio también lo está. Sin este cruce, borrar
    /// una plantilla dejaría bloqueado para siempre el recurso al que apuntaban sus ítems.
    /// </summary>
    public Task<int> CountItemsByTargetAsync(Guid targetId, CancellationToken cancellationToken = default)
        => dbContext.PermissionTemplateItems.CountAsync(
            i => i.TargetId == targetId
                 && i.IsActive
                 && dbContext.PermissionTemplates.Any(t => t.Id == i.TemplateId && t.StatusId != DeletedStatusId),
            cancellationToken);

    public void Dispose() => dbContext.Dispose();

    private static PermissionTemplateAggregate Rehydrate(PermissionTemplateRecord record)
        => AuthorizationAggregateFactory.RehydratePermissionTemplate(record, record.Items);

    private static PermissionTemplateRecord ToRecord(PermissionTemplateAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new PermissionTemplateRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            TenantId = aggregate.Props.TenantId.GetValue(),
            RoleId = aggregate.Props.RoleId.GetValue(),
            SystemSuiteId = aggregate.Props.SystemSuiteId.GetValue(),
            Version = aggregate.Props.Version.GetValue(),
            StatusId = aggregate.Props.Status.Id,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
            Items = aggregate.Items.Select(x =>
            {
                var a = x.Props.Audit.GetValue();
                return new PermissionTemplateItemRecord
                {
                    Id = x.Props.Id.GetValue(),
                    TemplateId = x.Props.TemplateId.GetValue(),
                    TargetTypeId = x.Props.TargetType.Id,
                    TargetId = x.Props.TargetId.GetValue(),
                    ActionId = x.Props.ActionId.GetValue(),
                    IsAllowed = x.Props.IsAllowed,
                    IsDenied = x.Props.IsDenied,
                    IsActive = x.Props.IsActive,
                    CreatedBy = a.CreatedBy,
                    CreatedAtUtc = a.CreatedAt,
                    UpdatedBy = a.UpdatedBy,
                    UpdatedAtUtc = a.UpdatedAt,
                    AuditTimeSpan = a.TimeSpan,
                };
            }).ToList(),
        };
    }

    private void Apply(PermissionTemplateRecord target, PermissionTemplateAggregate source)
    {
        var replacement = ToRecord(source);

        target.TenantId = replacement.TenantId;
        target.RoleId = replacement.RoleId;
        target.SystemSuiteId = replacement.SystemSuiteId;
        target.Version = replacement.Version;
        target.StatusId = replacement.StatusId;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;

        EfChildCollectionReconciler.ReconcileById(
            dbContext,
            target.Items,
            replacement.Items,
            item => item.Id,
            UpdateItem);
    }

    private static void UpdateItem(PermissionTemplateItemRecord target, PermissionTemplateItemRecord source)
    {
        target.TemplateId = source.TemplateId;
        target.TargetTypeId = source.TargetTypeId;
        target.TargetId = source.TargetId;
        target.ActionId = source.ActionId;
        target.IsAllowed = source.IsAllowed;
        target.IsDenied = source.IsDenied;
        target.IsActive = source.IsActive;
        target.CreatedBy = source.CreatedBy;
        target.CreatedAtUtc = source.CreatedAtUtc;
        target.UpdatedBy = source.UpdatedBy;
        target.UpdatedAtUtc = source.UpdatedAtUtc;
        target.AuditTimeSpan = source.AuditTimeSpan;
    }
}
