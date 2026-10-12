using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization;

using MenuNodeEntity = Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode.MenuNode;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;

public sealed class PostgreSqlSystemSuiteRepository(UmsPlatformDbContext dbContext) : ISystemSuiteRepository, IUnitOfWork
{
    private readonly HashSet<SystemSuiteAggregate> _trackedAggregates = [];

    public IUnitOfWork UnitOfWork => this;

    public async Task<SystemSuiteAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.SystemSuites
            .AsSplitQuery()
            .Include(x => x.Modules).ThenInclude(m => m.Nodes).ThenInclude(n => n.Actions)
            .Include(x => x.AppSettings)
            .Include(x => x.Actions)
            .Include(x => x.DomainResources)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<SystemSuiteAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.SystemSuites
            .AsSplitQuery()
            .Include(x => x.Modules).ThenInclude(m => m.Nodes).ThenInclude(n => n.Actions)
            .Include(x => x.AppSettings)
            .Include(x => x.Actions)
            .Include(x => x.DomainResources)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    /// <summary>
    /// Sonda de unicidad del código, no una lectura del catálogo: la usa el alta para no chocar con
    /// el índice único <c>(TenantId, Code)</c>.
    ///
    /// <para>Es la ÚNICA consulta que ve también los sistemas eliminados lógicamente, y tiene que
    /// verlos: la lápida conserva su código y el índice único de PostgreSQL la sigue contando. Si
    /// esta sonda la ignorara, reutilizar el código de un sistema eliminado pasaría la validación y
    /// reventaría después con una violación de integridad —un 500 en vez de un 409 con sentido—.
    /// Se apaga solo el filtro de borrado; el de inquilino sigue puesto, porque la unicidad es por
    /// inquilino y no debe filtrarse la existencia de códigos ajenos (G-246).</para>
    /// </summary>
    public async Task<SystemSuiteAggregate?> GetByCodeAsync(Code code, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.SystemSuites
            .IgnoreQueryFilters([UmsPlatformDbContext.SystemSuiteSoftDeleteFilter])
            .AsSplitQuery()
            .Include(x => x.Modules).ThenInclude(m => m.Nodes).ThenInclude(n => n.Actions)
            .Include(x => x.AppSettings)
            .Include(x => x.Actions)
            .Include(x => x.DomainResources)
            .FirstOrDefaultAsync(x => x.Code == code.GetValue(), cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<IReadOnlyList<SystemSuiteAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<SystemSuiteRecord> query = dbContext.SystemSuites.AsSplitQuery()
            .Include(x => x.Modules).ThenInclude(m => m.Nodes).ThenInclude(n => n.Actions)
            .Include(x => x.AppSettings)
            .Include(x => x.Actions)
            .Include(x => x.DomainResources);

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var records = await query.OrderBy(x => x.Code).ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<SystemSuiteAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.SystemSuites
            .AsSplitQuery()
            .Include(x => x.Modules).ThenInclude(m => m.Nodes).ThenInclude(n => n.Actions)
            .Include(x => x.AppSettings)
            .Include(x => x.Actions)
            .Include(x => x.DomainResources)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuiteSummary>> GetSummariesByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];

        // Proyección directa a columnas: ni `Include`, ni rehidratación, ni seguimiento de cambios.
        return await dbContext.SystemSuites
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuiteSummary(x.Id, x.Code, x.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuitePage> GetPageAsync(
        Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuitePageQuery query,
        CancellationToken cancellationToken = default)
    {
        // Sin Include: aquí solo se decide QUÉ suites entran en la página. El árbol de cada una
        // lo carga después `GetByIdsAsync`, y solo el de las que sobreviven al filtro.
        IQueryable<SystemSuiteRecord> consulta = dbContext.SystemSuites.AsNoTracking();

        if (query.TenantId.HasValue)
            consulta = consulta.Where(x => x.TenantId == query.TenantId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            // El estado se persiste como identificador, no como nombre: se traduce aquí para
            // filtrar por columna indexable en vez de por una expresión.
            var estadoId = DomainEnumerationMapper.FromName<Evolith.Ums.Domain.Enums.SystemStatus>(query.Status).Id;
            consulta = consulta.Where(x => x.StatusId == estadoId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var patron = $"%{query.Search}%";

            // ILIKE es exclusivo de PostgreSQL. En SQL Server la insensibilidad a mayúsculas la
            // aporta la COLLATION de la columna, así que LIKE basta siempre que la base se cree
            // con una collation CI (la instancia UAT usa Modern_Spanish_CI_AS). Con una collation
            // CS el filtro pasaría a ser sensible al caso sin avisar: por eso la migración fija
            // la collation de forma explícita en vez de heredar la del servidor.
            var esPostgres = dbContext.Database.IsNpgsql();

            consulta = (query.SearchField, esPostgres) switch
            {
                ("code", true)  => consulta.Where(x => EF.Functions.ILike(x.Code, patron)),
                ("code", false) => consulta.Where(x => EF.Functions.Like(x.Code, patron)),
                ("id", true)    => consulta.Where(x => EF.Functions.ILike(x.Id.ToString(), patron)),
                ("id", false)   => consulta.Where(x => EF.Functions.Like(x.Id.ToString(), patron)),
                (_, true)       => consulta.Where(x => EF.Functions.ILike(x.Name, patron)),
                (_, false)      => consulta.Where(x => EF.Functions.Like(x.Name, patron)),
            };
        }

        var total = await consulta.CountAsync(cancellationToken);

        consulta = (query.SortBy, query.Descending) switch
        {
            ("code", true)    => consulta.OrderByDescending(x => x.Code),
            ("code", false)   => consulta.OrderBy(x => x.Code),
            // Se ordena por el NOMBRE del estado, no por su identificador, para conservar el
            // orden que el listado ya mostraba (Active, Deprecated, Maintenance). EF lo traduce
            // a un CASE, sin traer filas.
            // La expresión va INLINE, no en un método: EF traduce un condicional a CASE, pero
            // no sabe traducir la llamada a un método propio — lo haría en memoria o reventaría.
            ("status", true)  => consulta.OrderByDescending(x => x.StatusId == 1 ? "Active" : x.StatusId == 2 ? "Maintenance" : "Deprecated"),
            ("status", false) => consulta.OrderBy(x => x.StatusId == 1 ? "Active" : x.StatusId == 2 ? "Maintenance" : "Deprecated"),
            (_, true)         => consulta.OrderByDescending(x => x.Name),
            _                 => consulta.OrderBy(x => x.Name),
        };

        var ids = await consulta
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        return new Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuitePage(ids, total);
    }

    public async Task<IReadOnlyList<SystemSuiteAggregate>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];

        var records = await dbContext.SystemSuites
            .AsSplitQuery()
            .Include(x => x.Modules).ThenInclude(m => m.Nodes).ThenInclude(n => n.Actions)
            .Include(x => x.AppSettings)
            .Include(x => x.Actions)
            .Include(x => x.DomainResources)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        // Se respeta el orden que decidió la página: `IN (...)` no lo garantiza.
        var porId = records.ToDictionary(r => r.Id);
        return ids.Where(porId.ContainsKey).Select(id => Rehydrate(porId[id])).ToList();
    }

    public async Task<Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuiteDependents> GetDependentsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Conteos, no cargas: la guarda solo necesita saber CUÁNTAS referencias vivas apuntan al
        // sistema. Traer los agregados para contarlos sería el patrón que ADR-0041 proscribe, y aquí
        // ni siquiera se mira un solo campo de ellos. Todas las columnas implicadas están indexadas.
        //
        // Sobre qué se considera «vivo» (regla de cascada, G-246): de estas siete tablas, solo
        // `Tenants` tiene eliminación lógica —su filtro global `!IsDeleted` deja fuera a los
        // inquilinos ya eliminados, de modo que un inquilino difunto NO bloquea—. Las otras seis no
        // tienen marca de borrado lógico: `Roles.IsActive`, `PermissionTemplates.StatusId`,
        // `FeatureFlags/IdpConfigurations/AppConfigurations` con su `Archived` y `ApprovalWorkflows`
        // sin estado alguno describen ciclo de vida, no borrado. Se cuenta TODA fila existente:
        // fail-closed. Cuando alguna de ellas gane su estado terminal, aquí se añade el predicado y
        // en ningún sitio más.
        var roles = await dbContext.Roles
            .CountAsync(x => x.SystemSuiteId == id, cancellationToken).ConfigureAwait(false);
        var templates = await dbContext.PermissionTemplates
            .CountAsync(x => x.SystemSuiteId == id, cancellationToken).ConfigureAwait(false);
        var featureFlags = await dbContext.FeatureFlags
            .CountAsync(x => x.SystemSuiteId == id, cancellationToken).ConfigureAwait(false);
        var idpConfigurations = await dbContext.IdpConfigurations
            .CountAsync(x => x.SystemSuiteId == id, cancellationToken).ConfigureAwait(false);
        var appConfigurations = await dbContext.AppConfigurations
            .CountAsync(x => x.SystemSuiteId == id, cancellationToken).ConfigureAwait(false);
        var approvalWorkflows = await dbContext.ApprovalWorkflows
            .CountAsync(x => x.SystemSuiteId == id, cancellationToken).ConfigureAwait(false);
        // Es la referencia más peligrosa: si el sistema por defecto de un inquilino deja de leerse,
        // su resolución de método de autenticación se queda sin destino. El filtro global de
        // `Tenants` (`!IsDeleted`) es el que materializa aquí la regla del propietario: un inquilino
        // ya eliminado lógicamente es una lápida y no cuenta.
        var tenantsUsingAsDefault = await dbContext.Tenants
            .CountAsync(x => x.DefaultSystemSuiteId == id, cancellationToken).ConfigureAwait(false);

        return new Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuiteDependents(
            roles,
            templates,
            featureFlags,
            idpConfigurations,
            appConfigurations,
            approvalWorkflows,
            tenantsUsingAsDefault);
    }

    // No hay DeleteAsync, y su ausencia es deliberada: la eliminación de un sistema es un cambio de
    // estado a `SystemStatus.Deleted` que viaja por `UpdateAsync`. Sobre el catálogo se consultan
    // datos antiguos, y una fila borrada de verdad no se recupera.

    public Task AddAsync(SystemSuiteAggregate aggregate, CancellationToken cancellationToken = default)
    {
        dbContext.SystemSuites.Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(SystemSuiteAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var id = aggregate.Props.Id.GetValue();

        // Prefer the already-tracked entity loaded by GetByIdAsync in the same request scope.
        // Issuing a second query returns the same instances via EF Core's identity map, but
        // the Clear()+Add(same-PK) pattern in Apply then creates a Deleted↔Added conflict in
        // the change tracker that causes a false DbUpdateConcurrencyException.
        var existing =
            dbContext.ChangeTracker
                .Entries<SystemSuiteRecord>()
                .FirstOrDefault(e => e.Entity.Id == id)
                ?.Entity
            ?? await dbContext.SystemSuites
                    .Include(x => x.Modules).ThenInclude(m => m.Nodes).ThenInclude(n => n.Actions)
                .Include(x => x.AppSettings)
                .Include(x => x.Actions)
                .Include(x => x.DomainResources)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"System suite {id} does not exist.");

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

        foreach (var aggregate in _trackedAggregates)
        {
            aggregate.DomainEvents.MarkChangesAsCommitted();
        }

        _trackedAggregates.Clear();
        return true;
    }

    public void Dispose() => dbContext.Dispose();

    private static SystemSuiteAggregate Rehydrate(SystemSuiteRecord record)
        => AuthorizationAggregateFactory.RehydrateSystemSuite(record, record.Modules, record.AppSettings, record.Actions, record.DomainResources);

    private static SystemSuiteRecord ToRecord(SystemSuiteAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new SystemSuiteRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            TenantId = aggregate.Props.TenantId.GetValue(),
            Code = aggregate.Props.Code.GetValue(),
            Name = aggregate.Props.Name.GetValue(),
            Description = aggregate.Props.Description.GetValue(),
            StatusId = aggregate.Props.Status.Id,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
            Modules = aggregate.Modules.Select(ToRecord).ToList(),
            AppSettings = aggregate.AppSettings.Select(x => new SystemSuiteAppSettingRecord
            {
                Id = Guid.NewGuid(),
                SystemSuiteId = aggregate.Props.Id.GetValue(),
                ConfigKey = x.Key.GetValue(),
                IsClientVisible = x.IsClientVisible,
                ConfigValue = x.Value.GetValue(),
                ScopeId = x.Scope.Id,
            }).ToList(),
            Actions = aggregate.Actions.Select(x =>
            {
                var a = x.Props.Audit.GetValue();
                return new SystemSuiteActionRecord
                {
                    Id = x.Props.Id.GetValue(),
                    TenantId = x.Props.TenantId.GetValue(),
                    SystemSuiteId = x.Props.SystemSuiteId!.GetValue(),
                    ModuleId = x.Props.ModuleId?.GetValue(),
                    Code = x.Props.Code.GetValue(),
                    Name = x.Props.Name.GetValue(),
                    CreatedBy = a.CreatedBy,
                    CreatedAtUtc = a.CreatedAt,
                    UpdatedBy = a.UpdatedBy,
                    UpdatedAtUtc = a.UpdatedAt,
                    AuditTimeSpan = a.TimeSpan,
                };
            }).ToList(),
            DomainResources = aggregate.DomainResources.Select(x =>
            {
                var a = x.Props.Audit.GetValue();
                return new SystemSuiteDomainResourceRecord
                {
                    Id = x.Props.Id.GetValue(),
                    SystemSuiteId = x.Props.SystemSuiteId.GetValue(),
                    ModuleId = x.Props.ModuleId?.GetValue(),
                    ParentResourceId = x.Props.ParentResourceId?.GetValue(),
                    Type = x.Props.Type.Name,
                    Code = x.Props.Code.GetValue(),
                    Name = x.Props.Name.GetValue(),
                    Description = x.Props.Description.GetValue(),
                    CreatedBy = a.CreatedBy,
                    CreatedAtUtc = a.CreatedAt,
                    UpdatedBy = a.UpdatedBy,
                    UpdatedAtUtc = a.UpdatedAt,
                    AuditTimeSpan = a.TimeSpan,
                };
            }).ToList(),
        };
    }

    private static SystemSuiteModuleRecord ToRecord(Evolith.Ums.Domain.Authorization.SystemSuite.Module.Module module)
    {
        var audit = module.Props.Audit.GetValue();
        return new SystemSuiteModuleRecord
        {
            Id = module.Props.Id.GetValue(),
            SystemSuiteId = module.Props.SystemId.GetValue(),
            Code = module.Props.Code.GetValue(),
            Name = module.Props.Name.GetValue(),
            Description = module.Props.Description.GetValue(),
            StatusId = module.Props.Status.Id,
            SortOrder = module.Props.SortOrder,
            Icon = module.Props.Icon,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
            Nodes = FlattenNodes(module.Nodes),
        };
    }

    private static SystemSuiteNodeRecord ToRecord(MenuNodeEntity node)
    {
        var audit = node.Props.Audit.GetValue();
        var meta = node.Metadata;
        var nodeId = node.GetId().GetValue();
        return new SystemSuiteNodeRecord
        {
            Id = nodeId,
            ModuleId = node.ModuleId.GetValue(),
            ParentNodeId = node.ParentNodeId?.GetValue(),
            NodeKindId = (int)node.Kind,
            Code = node.Code.GetValue(),
            Label = node.Label.GetValue(),
            Description = node.Description.GetValue(),
            StatusId = node.Status.Id,
            SortOrder = node.SortOrder,
            Icon = node.Props.Presentation.Icon,
            Route = node.Props.Presentation.Route,
            Responsable = meta.Responsable,
            Criticidad = meta.Criticidad,
            ProductoImpactado = meta.ProductoImpactado,
            ComponenteTecnico = meta.ComponenteTecnico,
            Dependencias = meta.Dependencias,
            Evidencias = meta.Evidencias,
            TrazabilidadSdlc = meta.TrazabilidadSdlc,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
            Actions = node.ActionCodes.Select(ac => new SystemSuiteNodeActionRecord
            {
                Id = Guid.NewGuid(),
                NodeId = nodeId,
                ActionCode = ac.GetValue(),
            }).ToList(),
        };
    }

    /// <summary>Aplana el árbol de MenuNode a filas planas (ParentNodeId enlaza el árbol).</summary>
    private static List<SystemSuiteNodeRecord> FlattenNodes(IEnumerable<MenuNodeEntity> roots)
    {
        var flat = new List<SystemSuiteNodeRecord>();

        void Walk(MenuNodeEntity node)
        {
            flat.Add(ToRecord(node));
            foreach (var child in node.Children)
            {
                Walk(child);
            }
        }

        foreach (var root in roots)
        {
            Walk(root);
        }

        return flat;
    }

    private void Apply(SystemSuiteRecord target, SystemSuiteAggregate source)
    {
        var replacement = ToRecord(source);

        // ── Scalar properties ────────────────────────────────────────────────
        target.TenantId    = replacement.TenantId;
        target.Code        = replacement.Code;
        target.Name        = replacement.Name;
        target.Description = replacement.Description;
        target.StatusId    = replacement.StatusId;
        target.CreatedBy   = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy   = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;

        // ── Modules ───────────────────────────────────────────────────────────
        ReconcileModules(target.Modules, replacement.Modules);

        // ── AppSettings ───────────────────────────────────────────────────────
        // Key: (ConfigKey, ScopeId) — NOT Id.
        // ToRecord() generates Id = Guid.NewGuid() on every call, so using Id
        // as the key would always treat every existing setting as Deleted and every
        // replacement as a new INSERT, causing DbUpdateConcurrencyException when
        // EF Core finds a non-default GUID that doesn't exist in the database.
        ReconcileByKey(
            target.AppSettings,
            replacement.AppSettings,
            s => (s.ConfigKey, s.ScopeId),
            (existing, rep) =>
            {
                existing.ConfigKey   = rep.ConfigKey;
                existing.ConfigValue = rep.ConfigValue;
                existing.ScopeId     = rep.ScopeId;
            });

        // ── Actions ───────────────────────────────────────────────────────────
        ReconcileByKey(
            target.Actions,
            replacement.Actions,
            a => a.Id,
            (existing, rep) =>
            {
                existing.Code        = rep.Code;
                existing.Name        = rep.Name;
                existing.ModuleId    = rep.ModuleId;
                existing.UpdatedBy   = rep.UpdatedBy;
                existing.UpdatedAtUtc = rep.UpdatedAtUtc;
                existing.AuditTimeSpan = rep.AuditTimeSpan;
            });

        // ── DomainResources ───────────────────────────────────────────────────
        ReconcileByKey(
            target.DomainResources,
            replacement.DomainResources,
            d => d.Id,
            (existing, rep) =>
            {
                existing.ModuleId    = rep.ModuleId;
                existing.Type        = rep.Type;
                existing.Code        = rep.Code;
                existing.Name        = rep.Name;
                existing.Description = rep.Description;
                existing.UpdatedBy   = rep.UpdatedBy;
                existing.UpdatedAtUtc = rep.UpdatedAtUtc;
                existing.AuditTimeSpan = rep.AuditTimeSpan;
            });
    }

    /// <summary>
    /// Reconciles a tracked EF Core child collection against a replacement set.
    ///
    /// EF Core change-tracking subtlety (FIX-09):
    /// When a new entity with a manually-set non-default GUID is added to a tracked
    /// navigation collection, EF Core's <c>DetectChanges()</c> may assign
    /// <c>EntityState.Modified</c> (assuming the GUID already exists in the database)
    /// instead of <c>EntityState.Added</c>. This causes an UPDATE on a non-existent row
    /// → 0 rows affected → false <c>DbUpdateConcurrencyException</c>.
    ///
    /// The fix: explicitly set <c>EntityState.Added</c> on every genuinely new entity
    /// so EF Core always generates INSERT, never UPDATE.
    /// </summary>
    private void ReconcileByKey<TEntity, TKey>(
        IList<TEntity> tracked,
        IList<TEntity> replacement,
        Func<TEntity, TKey> keySelector,
        Action<TEntity, TEntity> update)
        where TKey : notnull
    {
        var replacementByKey = replacement.ToDictionary(keySelector);
        var trackedByKey     = tracked.ToDictionary(keySelector);

        // Remove entities no longer present in the aggregate.
        foreach (var (key, entity) in trackedByKey)
        {
            if (!replacementByKey.ContainsKey(key))
                tracked.Remove(entity);
        }

        // Update existing in-place or explicitly INSERT new.
        foreach (var (key, repEntity) in replacementByKey)
        {
            if (trackedByKey.TryGetValue(key, out var existingEntity))
            {
                update(existingEntity, repEntity);   // update in-place — no PK conflict
            }
            else
            {
                tracked.Add(repEntity);

                // FIX-09: Force EntityState.Added so EF Core generates INSERT.
                // DetectChanges() alone would assign Modified for entities with a
                // pre-set non-default GUID, trying to UPDATE a row that does not exist.
                dbContext.Entry(repEntity).State = EntityState.Added;
            }
        }
    }

    /// <summary>
    /// Reconciles the Modules collection and recursively reconciles all
    /// Menus → SubMenus → Options within each module.
    /// </summary>
    private void ReconcileModules(
        IList<SystemSuiteModuleRecord> tracked,
        IList<SystemSuiteModuleRecord> replacement)
    {
        ReconcileByKey(
            tracked,
            replacement,
            m => m.Id,
            (existing, rep) =>
            {
                existing.Code          = rep.Code;
                existing.Name          = rep.Name;
                existing.Description   = rep.Description;
                existing.StatusId      = rep.StatusId;
                existing.SortOrder     = rep.SortOrder;
                existing.UpdatedBy     = rep.UpdatedBy;
                existing.UpdatedAtUtc  = rep.UpdatedAtUtc;
                existing.AuditTimeSpan = rep.AuditTimeSpan;

                ReconcileNodes(existing.Nodes, rep.Nodes);
            });
    }

    /// <summary>
    /// Reconcilia el árbol de nodos en PLANO por Id (el árbol se enlaza por
    /// ParentNodeId). Cubre inserción, actualización y borrado de subárboles.
    /// </summary>
    private void ReconcileNodes(
        IList<SystemSuiteNodeRecord> tracked,
        IList<SystemSuiteNodeRecord> replacement)
    {
        ReconcileByKey(
            tracked,
            replacement,
            n => n.Id,
            (existing, rep) =>
            {
                existing.ParentNodeId      = rep.ParentNodeId;
                existing.NodeKindId        = rep.NodeKindId;
                existing.Code              = rep.Code;
                existing.Label             = rep.Label;
                existing.Description       = rep.Description;
                existing.StatusId          = rep.StatusId;
                existing.SortOrder         = rep.SortOrder;
                existing.Responsable       = rep.Responsable;
                existing.Criticidad        = rep.Criticidad;
                existing.ProductoImpactado = rep.ProductoImpactado;
                existing.ComponenteTecnico = rep.ComponenteTecnico;
                existing.Dependencias      = rep.Dependencias;
                existing.Evidencias        = rep.Evidencias;
                existing.TrazabilidadSdlc  = rep.TrazabilidadSdlc;
                existing.UpdatedBy         = rep.UpdatedBy;
                existing.UpdatedAtUtc      = rep.UpdatedAtUtc;
                existing.AuditTimeSpan     = rep.AuditTimeSpan;

                ReconcileNodeActions(existing.Actions, rep.Actions);
            });
    }

    private void ReconcileNodeActions(
        IList<SystemSuiteNodeActionRecord> tracked,
        IList<SystemSuiteNodeActionRecord> replacement)
    {
        // Id se regenera en cada ToRecord → reconciliar por ActionCode (la clave real).
        ReconcileByKey(
            tracked,
            replacement,
            a => a.ActionCode,
            (_, _) => { /* solo la clave es relevante; nada mutable que actualizar */ });
    }

}
