using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration;

using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;

public sealed class PostgreSqlAppConfigurationRepository(UmsPlatformDbContext dbContext) : IAppConfigurationRepository, IUnitOfWork
{
    private readonly HashSet<AppConfigurationAggregate> _trackedAggregates = [];

    public IUnitOfWork UnitOfWork => this;

    // Las lecturas ocultan lo eliminado lógicamente: la fila permanece en la tabla —la consulta
    // histórica la necesita— pero un GET sobre ella responde 404 y no aparece en ningún listado.
    public async Task<AppConfigurationAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.AppConfigurations
            .FirstOrDefaultAsync(x => x.Id == id && x.StatusId != ConfigStatus.Deleted.Id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public Task<AppConfigurationAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    /// <summary>
    /// Lookup por ámbito+código: devuelve quién OCUPA esa ranura ahora mismo, nunca una lápida.
    ///
    /// Antes no filtraba lo eliminado, porque el índice único tampoco lo hacía y ocultarlo habría
    /// convertido un conflicto legible en un 23505 → 500. Ahora el índice es parcial (solo filas
    /// vivas), así que la comprobación de unicidad tiene que mirar exactamente lo mismo que mira el
    /// índice: si siguiera viendo la lápida, el alta devolvería 409 sobre una ranura que la base
    /// considera libre. Es el 409-donde-se-esperaba-201 que midió la certificación E2E.
    ///
    /// El orden hace la respuesta DETERMINISTA cuando conviven varias filas con el mismo código:
    /// una viva y N eliminadas es el caso normal tras liberar la ranura, y las eliminadas ya quedan
    /// fuera; el desempate por estado y fecha cubre además el caso —permitido por el handler desde
    /// CFG-06— de una archivada conviviendo con la nueva: gana la que no está archivada, y entre
    /// iguales la más reciente. Sin ese orden, `FirstOrDefault` devolvería una fila arbitraria.
    /// </summary>
    public async Task<AppConfigurationAggregate?> GetByScopeAndCodeAsync(Guid? tenantId, Guid? systemSuiteId, Guid? moduleId, string code, CancellationToken cancellationToken = default)
    {
        IQueryable<AppConfigurationRecord> query = dbContext.AppConfigurations;

        if (tenantId.HasValue)
        {
            query = query.IgnoreQueryFilters();
        }

        var deletedStatusId = ConfigStatus.Deleted.Id;
        var archivedStatusId = ConfigStatus.Archived.Id;

        var record = await query
            .Where(x =>
                x.TenantId == tenantId
                && x.SystemSuiteId == systemSuiteId
                && x.ModuleId == moduleId
                && x.Code == code
                && x.StatusId != deletedStatusId)
            .OrderBy(x => x.StatusId == archivedStatusId ? 1 : 0)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<IReadOnlyList<AppConfigurationAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<AppConfigurationRecord> query = dbContext.AppConfigurations;

        if (tenantId.HasValue)
        {
            query = query.IgnoreQueryFilters().Where(x => x.TenantId == tenantId.Value);
        }

        // Lo eliminado lógicamente no se lista (ni en la API ni en la carga de la caché).
        var records = await query
            .Where(x => x.StatusId != ConfigStatus.Deleted.Id)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public Task AddAsync(AppConfigurationAggregate aggregate, CancellationToken cancellationToken = default)
    {
        dbContext.AppConfigurations.Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(AppConfigurationAggregate aggregate, CancellationToken cancellationToken = default)
        => await UpdateAsync(aggregate, expectedRowVersion: null, cancellationToken);

    /// <inheritdoc cref="IAppConfigurationRepository.UpdateAsync(AppConfigurationAggregate, byte[], CancellationToken)"/>
    public async Task UpdateAsync(
        AppConfigurationAggregate aggregate,
        byte[]? expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.AppConfigurations
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"AppConfiguration {aggregate.Props.Id.GetValue()} does not exist.");

        Apply(existing, aggregate);

        // REC-10: If the caller supplied a RowVersion from the If-Match header, override EF Core's
        // tracked "original" value so the generated UPDATE includes a WHERE RowVersion = @client_rv.
        // A concurrent modification will cause 0 rows affected → DbUpdateConcurrencyException → HTTP 409.
        if (expectedRowVersion is { Length: > 0 })
        {
            dbContext.Entry(existing).Property(x => x.RowVersion).OriginalValue = expectedRowVersion;
        }

        _trackedAggregates.Add(aggregate);
    }

    // Aquí NO hay DeleteAsync: el borrado es lógico y viaja por UpdateAsync como cualquier otro
    // cambio de estado (StatusId → Deleted). Ninguna ruta de este repositorio retira filas.

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
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            // FIX-03: surface optimistic concurrency failures as 409 Conflict
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

    public void Dispose() => dbContext.Dispose();

    private static AppConfigurationAggregate Rehydrate(AppConfigurationRecord record)
        => ConfigurationAggregateFactory.RehydrateAppConfiguration(record);

    private static AppConfigurationRecord ToRecord(AppConfigurationAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new AppConfigurationRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            TenantId = aggregate.Props.TenantId?.GetValue(),
            SystemSuiteId = aggregate.Props.SystemSuiteId?.GetValue(),
            ModuleId = aggregate.Props.ModuleId?.GetValue(),
            Code = aggregate.Props.Code.GetValue(),
            Value = aggregate.Props.Value.GetValue(),
            Description = aggregate.Props.Description.GetValue(),
            ScopeId = aggregate.Props.Scope.Id,
            IsInheritable = aggregate.Props.IsInheritable,
            IsEncrypted = aggregate.Props.IsEncrypted,
            IsNonOverridable = aggregate.Props.IsNonOverridable,
            Version = aggregate.Props.Version,
            StatusId = aggregate.Props.Status.Id,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
        };
    }

    private static void Apply(AppConfigurationRecord target, AppConfigurationAggregate source)
    {
        var replacement = ToRecord(source);

        target.TenantId = replacement.TenantId;
        target.SystemSuiteId = replacement.SystemSuiteId;
        target.ModuleId = replacement.ModuleId;
        target.Code = replacement.Code;
        target.Value = replacement.Value;
        target.Description = replacement.Description;
        target.ScopeId = replacement.ScopeId;
        target.IsInheritable = replacement.IsInheritable;
        target.IsEncrypted = replacement.IsEncrypted;
        target.IsNonOverridable = replacement.IsNonOverridable;
        target.Version = replacement.Version;
        target.StatusId = replacement.StatusId;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;
    }
}
