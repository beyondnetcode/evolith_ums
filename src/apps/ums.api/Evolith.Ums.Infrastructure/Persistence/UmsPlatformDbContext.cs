using Evolith.Ums.Infrastructure.Persistence.Approvals.Configurations;
using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;
using Evolith.Ums.Infrastructure.Persistence.Audit.Configurations;
using Evolith.Ums.Infrastructure.Persistence.Audit.Entities;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Configurations;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Configurations;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Evolith.Ums.Infrastructure.Persistence.Identity.Configurations;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using Evolith.Ums.Infrastructure.Persistence.Iga.Configurations;
using Evolith.Ums.Infrastructure.Persistence.Iga.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Evolith.Ums.Infrastructure.Persistence;

/// <summary>
/// Central EF Core DbContext for the UMS platform.
///
/// Tenant isolation (FIX-05):
/// All tenant-scoped entities carry a <c>TenantId</c> column. A global query filter is
/// applied to each of them so that EF never emits a SQL query without a WHERE clause
/// restricting to the caller's <see cref="ITenantContext.OrganizationId"/>.
///
/// Filter semantics:
/// - <c>OrganizationId == null</c> (system / background context) → no filter applied,
///   all rows visible (needed for the outbox dispatcher, dev seeding, admin ops).
/// - <c>OrganizationId.HasValue</c>                              → strict per-tenant rows only.
/// - <see cref="AppConfigurationRecord"/> is nullable-TenantId    → also includes global
///   records (<c>TenantId IS NULL</c>) so system-level config is always visible.
///
/// Tenant isolation is enforced entirely by these application-layer query filters under the
/// PostgreSQL-only persistence model.
/// </summary>
public sealed class UmsPlatformDbContext(
    DbContextOptions<UmsPlatformDbContext> options,
    ITenantContext tenantContext,
    IPublisher domainEventDispatcher,
    ILogger<UmsPlatformDbContext> logger) : DbContext(options)
{
    public const string DefaultSchema = "ums_platform";

    // G-246: identificador del estado terminal `SystemStatus.Deleted`, para el filtro global que
    // oculta los sistemas eliminados lógicamente. Se lee del propio enumerado del dominio —no se
    // escribe el 4 a mano— para que no puedan divergir; se materializa en un campo porque la
    // expresión del filtro debe quedar traducible a SQL, no invocar una cadena de propiedades.
    private static readonly int DeletedSystemSuiteStatusId = Evolith.Ums.Domain.Enums.SystemStatus.Deleted.Id;

    /// <summary>Nombre del filtro que oculta los sistemas eliminados lógicamente (G-246).</summary>
    public const string SystemSuiteSoftDeleteFilter = "SystemSuiteSoftDelete";

    /// <summary>Nombre del filtro de aislamiento por inquilino de los sistemas.</summary>
    public const string SystemSuiteTenantFilter = "SystemSuiteTenant";

    // G-066 / ADR-0098 D4/D7 + KB-TXN-001: los eventos de DOMINIO se manejan EN PROCESO. Se
    // recolectan aquí durante SaveEntitiesAsync y se despachan por MediatR DESPUÉS del commit del
    // agregado (en el override de SaveChangesAsync). NO se publican crudos al bróker inter-sistema
    // (D7/D9.6): eso convertiría el modelo interno en contrato de transporte. Solo los eventos de
    // INTEGRACIÓN explícitos (IIntegrationEvent, contrato aparte) salen al bróker por el outbox
    // (IIntegrationEventPublisher).
    private readonly List<BeyondNetCode.Shell.Ddd.Interfaces.IDomainEvent> _pendingDomainEvents = [];
    private bool _isDispatchingDomainEvents;

    /// <summary>
    /// Recolecta los eventos de dominio no confirmados de un agregado para despacharlos EN PROCESO
    /// tras el commit (ADR-0098 D4.2). No publica al bróker (D7/D9.6). Los repositorios llaman a
    /// este método ANTES de <see cref="SaveChangesAsync(bool, CancellationToken)"/>; el despacho
    /// real ocurre en el override de SaveChangesAsync, una vez confirmada la transacción del
    /// agregado.
    /// </summary>
    public Task PublishDomainEventsAsync(IEnumerable<BeyondNetCode.Shell.Ddd.Interfaces.IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        _pendingDomainEvents.AddRange(domainEvents);
        return Task.CompletedTask;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        await DispatchDomainEventsAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        DispatchDomainEventsAsync(CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    /// <summary>
    /// Despacha en proceso, tras el commit, los eventos de dominio recolectados (ADR-0098 D4). Un
    /// fallo de un manejador en proceso NO revierte el agregado ya confirmado ni tumba el caso de
    /// uso: es un handoff post-commit reconstruible/best-effort (D4/D6), así que se registra como
    /// advertencia. Los efectos durables e irreparables (auditoría, no-repudiación) NO viajan por
    /// esta vía: usan el outbox (G-040).
    ///
    /// TODO(D-016): para los casos de uso multi-agregado con [TransactionAspect] (excepciones de
    /// consistencia inmediata gobernadas por D-016), este despacho ocurre en el SaveChanges interno,
    /// antes del commit de la transacción externa. Revisar al separar esos agregados (ADR-0098 D2).
    /// </summary>
    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        // Guarda de reentrada: un manejador en proceso puede provocar otro SaveChanges sobre este
        // mismo contexto; no re-despachar dentro de un despacho en curso.
        if (_isDispatchingDomainEvents || _pendingDomainEvents.Count == 0)
            return;

        _isDispatchingDomainEvents = true;
        try
        {
            var toDispatch = _pendingDomainEvents.ToArray();
            _pendingDomainEvents.Clear();

            foreach (var domainEvent in toDispatch)
            {
                try
                {
                    await domainEventDispatcher.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Handoff en proceso fallido para el evento de dominio {DomainEvent} (post-commit, ADR-0098 D4). El agregado ya está confirmado; el efecto es reconstruible.",
                        domainEvent.GetType().Name);
                }
            }
        }
        finally
        {
            _isDispatchingDomainEvents = false;
        }
    }

    public DbSet<TenantRecord> Tenants => Set<TenantRecord>();
    public DbSet<TenantBranchRecord> TenantBranches => Set<TenantBranchRecord>();
    /// <summary>ADR-0164: bitácora de episodios de sucursal (apertura, baja, reapertura, cierre).</summary>
    public DbSet<TenantBranchLifecycleEntryRecord> TenantBranchLifecycleEntries => Set<TenantBranchLifecycleEntryRecord>();
    public DbSet<TenantIdentityProviderRecord> TenantIdentityProviders => Set<TenantIdentityProviderRecord>();
    public DbSet<TenantParameterRecord> TenantParameters => Set<TenantParameterRecord>();
    public DbSet<TenantSignupRequestRecord> TenantSignupRequests => Set<TenantSignupRequestRecord>();
    public DbSet<UserAccountRecord> UserAccounts => Set<UserAccountRecord>();
    public DbSet<UserAccountMfaEnrollmentRecord> UserAccountMfaEnrollments => Set<UserAccountMfaEnrollmentRecord>();
    public DbSet<UserAccountPasswordCredentialRecord> UserAccountPasswordCredentials => Set<UserAccountPasswordCredentialRecord>();
    public DbSet<ProfileRecord> Profiles => Set<ProfileRecord>();
    public DbSet<ProfilePermissionRecord> ProfilePermissions => Set<ProfilePermissionRecord>();
    public DbSet<SystemSuiteRecord> SystemSuites => Set<SystemSuiteRecord>();
    public DbSet<SystemSuiteModuleRecord> SystemSuiteModules => Set<SystemSuiteModuleRecord>();
    public DbSet<SystemSuiteNodeRecord> SystemSuiteNodes => Set<SystemSuiteNodeRecord>();
    public DbSet<SystemSuiteNodeActionRecord> SystemSuiteNodeActions => Set<SystemSuiteNodeActionRecord>();
    public DbSet<SystemSuiteAppSettingRecord> SystemSuiteAppSettings => Set<SystemSuiteAppSettingRecord>();
    public DbSet<SystemSuiteActionRecord> SystemSuiteActions => Set<SystemSuiteActionRecord>();
    public DbSet<SystemSuiteDomainResourceRecord> SystemSuiteDomainResources => Set<SystemSuiteDomainResourceRecord>();
    public DbSet<PermissionTemplateRecord> PermissionTemplates => Set<PermissionTemplateRecord>();
    public DbSet<PermissionTemplateItemRecord> PermissionTemplateItems => Set<PermissionTemplateItemRecord>();
    public DbSet<TemplateAssignmentRuleRecord> TemplateAssignmentRules => Set<TemplateAssignmentRuleRecord>();
    public DbSet<RoleRecord> Roles => Set<RoleRecord>();
    public DbSet<UserManagementDelegationRecord> UserManagementDelegations => Set<UserManagementDelegationRecord>();
    public DbSet<AppConfigurationRecord> AppConfigurations => Set<AppConfigurationRecord>();
    public DbSet<ParameterDefinitionRecord> ParameterDefinitions => Set<ParameterDefinitionRecord>();
    public DbSet<ParameterGlobalValueRecord> ParameterGlobalValues => Set<ParameterGlobalValueRecord>();
    public DbSet<ParameterTenantValueRecord> ParameterTenantValues => Set<ParameterTenantValueRecord>();
    public DbSet<FeatureFlagRecord> FeatureFlags => Set<FeatureFlagRecord>();
    public DbSet<FeatureFlagEvaluationLogRecord> FeatureFlagEvaluationLogs => Set<FeatureFlagEvaluationLogRecord>();
    public DbSet<FeatureFlagCriteriaRecord> FeatureFlagCriteria => Set<FeatureFlagCriteriaRecord>();
    public DbSet<IdpConfigurationRecord> IdpConfigurations => Set<IdpConfigurationRecord>();
    public DbSet<AuditRecordRecord> AuditRecords => Set<AuditRecordRecord>();
    public DbSet<ApprovalWorkflowRecord> ApprovalWorkflows => Set<ApprovalWorkflowRecord>();
    public DbSet<ApprovalRequiredDocumentRecord> ApprovalRequiredDocuments => Set<ApprovalRequiredDocumentRecord>();
    public DbSet<ApprovalRequestRecord> ApprovalRequests => Set<ApprovalRequestRecord>();
    public DbSet<NotificationRuleRecord> NotificationRules => Set<NotificationRuleRecord>();
    public DbSet<DocumentTypeRecord> DocumentTypes => Set<DocumentTypeRecord>();
    public DbSet<UserDocumentRecord> UserDocuments => Set<UserDocumentRecord>();
    public DbSet<AccessNotificationRecord> UserDocumentNotifications => Set<AccessNotificationRecord>();
    public DbSet<AccessEnforcementPolicyRecord> AccessEnforcementPolicies => Set<AccessEnforcementPolicyRecord>();
    public DbSet<RefreshTokenRecord> RefreshTokens => Set<RefreshTokenRecord>();
    public DbSet<PasswordResetTokenRecord> PasswordResetTokens => Set<PasswordResetTokenRecord>();
    public DbSet<RoleMaturityStatusRecord> RoleMaturityStatuses => Set<RoleMaturityStatusRecord>();
    public DbSet<RolePromotionRequestRecord> RolePromotionRequests => Set<RolePromotionRequestRecord>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        base.OnConfiguring(optionsBuilder);
    }

    /// <summary>
    /// Traduce el predicado de un índice parcial de PostgreSQL a la sintaxis de índice filtrado
    /// de SQL Server. Solo cubre las formas que usa el modelo; no pretende ser un traductor de SQL.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex IdentificadoresEntrecomillados =
        new("\"([^\"]+)\"",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1));

    private static string TraducirFiltroASqlServer(string filtro) =>
        IdentificadoresEntrecomillados
            .Replace(filtro, "[$1]")
            .Replace(" = true", " = 1", StringComparison.Ordinal)
            .Replace(" = false", " = 0", StringComparison.Ordinal)
            .Replace("!=", "<>", StringComparison.Ordinal);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);

        if (Database.IsNpgsql())
        {
            modelBuilder.HasPostgresExtension("pgcrypto");
        }

        modelBuilder.ApplyConfiguration(new TenantRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TenantBranchRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TenantBranchLifecycleEntryRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TenantIdentityProviderRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TenantParameterRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TenantSignupRequestRecordConfiguration());
        modelBuilder.ApplyConfiguration(new UserAccountRecordConfiguration());
        modelBuilder.ApplyConfiguration(new UserAccountMfaEnrollmentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new UserAccountPasswordCredentialRecordConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordResetTokenRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProfileRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProfilePermissionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SystemSuiteRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SystemSuiteModuleRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SystemSuiteNodeRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SystemSuiteNodeActionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SystemSuiteAppSettingRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SystemSuiteActionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SystemSuiteDomainResourceRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionTemplateRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionTemplateItemRecordConfiguration());
        modelBuilder.ApplyConfiguration(new RoleRecordConfiguration());
        modelBuilder.ApplyConfiguration(new UserManagementDelegationRecordConfiguration());
        modelBuilder.ApplyConfiguration(new AppConfigurationRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ParameterDefinitionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ParameterGlobalValueRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ParameterTenantValueRecordConfiguration());
        modelBuilder.ApplyConfiguration(new FeatureFlagRecordConfiguration());
        modelBuilder.ApplyConfiguration(new FeatureFlagEvaluationLogRecordConfiguration());
        modelBuilder.ApplyConfiguration(new FeatureFlagCriteriaRecordConfiguration());
        modelBuilder.ApplyConfiguration(new IdpConfigurationRecordConfiguration());
        modelBuilder.ApplyConfiguration(new AuditRecordRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ApprovalWorkflowRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ApprovalRequiredDocumentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ApprovalRequestRecordConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationRuleRecordConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentTypeRecordConfiguration());
        modelBuilder.ApplyConfiguration(new UserDocumentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new AccessNotificationRecordConfiguration());
        modelBuilder.ApplyConfiguration(new AccessEnforcementPolicyRecordConfiguration());
        modelBuilder.ApplyConfiguration(new RoleMaturityStatusRecordConfiguration());
        modelBuilder.ApplyConfiguration(new RolePromotionRequestRecordConfiguration());

        // G-066 / ADR-0098 D4: MassTransit transactional bus-outbox for the WRITE context.
        // Domain events are staged into the outbox tables and committed atomically with the
        // aggregate change set; delivery to the broker happens AFTER commit (no «phantom
        // messages»). Mirrors the pattern already used by TenantProjectionDbContext.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        // -------------------------------------------------------------------------
        // FIX-05: Global query filters — primary tenant isolation mechanism.
        //
        // When OrganizationId is null (system/background context) the short-circuit
        // !HasValue condition makes EF emit no WHERE clause, so all rows are visible.
        // When OrganizationId is set, only the current tenant's rows are returned.
        //
        // REC-16: Soft-delete filters are combined here so no deleted rows ever leak.
        // -------------------------------------------------------------------------

        // TenantRecord is not tenant-scoped (it IS the tenant) but must be soft-delete filtered.
        modelBuilder.Entity<TenantRecord>()
            .HasQueryFilter(x => !x.IsDeleted);

        // ADR-0164: la sucursal CERRADA no lleva filtro global de ocultación, y es deliberado. La
        // resolución por id tiene que seguir devolviéndola —el perfil de un usuario apunta a la
        // sucursal desde la que operó, y el grafo de autorización la resuelve por ese id— y la vía
        // de escritura necesita verla para que su código siga ocupado (§2.3). Quien LISTA para un
        // humano filtra en su consulta; ver `GetBranchesByTenantIdQueryHandler`. Un filtro global
        // aquí habría convertido el borrado lógico en una desaparición igual de opaca que el físico.
        modelBuilder.Entity<TenantBranchRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        modelBuilder.Entity<TenantBranchLifecycleEntryRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        // El borrado de parámetros es LÓGICO: el filtro de soft-delete se combina con el de inquilino
        // para que ninguna lectura —repositorio, provider o resolución de configuración— vea una fila
        // eliminada. Quien necesite el histórico debe pedirlo explícitamente con IgnoreQueryFilters().
        modelBuilder.Entity<TenantParameterRecord>()
            .HasQueryFilter(x =>
                !x.IsDeleted &&
                (!tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId));

        modelBuilder.Entity<TenantIdentityProviderRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        modelBuilder.Entity<UserAccountRecord>()
            .HasQueryFilter(x =>
                !x.IsDeleted &&
                (!tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId));

        modelBuilder.Entity<ProfileRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        modelBuilder.Entity<RoleRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        // G-246: un sistema eliminado lógicamente (StatusId = SystemStatus.Deleted) sigue en la tabla
        // —el catálogo se consulta sobre datos antiguos— pero no debe aparecer en ninguna lectura: el
        // GET devuelve 404 y el listado no lo trae. El filtro va aquí, y no repartido por consulta,
        // para que ninguna consulta futura pueda olvidarse de él; misma decisión que ya se tomó con
        // `UserAccountRecord.IsDeleted`. La escritura no se ve afectada: los filtros solo aplican a
        // consultas, y el UPDATE que marca el borrado sale de la entidad ya rastreada.
        //
        // Van como filtros CON NOMBRE (EF Core 10) y no como una sola expresión porque hay una
        // consulta —y solo una— que necesita apagar el de borrado sin apagar el de inquilino: la
        // comprobación de código duplicado del alta, que debe ver también las lápidas porque el
        // índice único de la base las sigue viendo. Con un filtro único, `IgnoreQueryFilters()` los
        // apagaría los dos y abriría el catálogo de todos los inquilinos.
        modelBuilder.Entity<SystemSuiteRecord>()
            .HasQueryFilter(SystemSuiteSoftDeleteFilter, x => x.StatusId != DeletedSystemSuiteStatusId)
            .HasQueryFilter(SystemSuiteTenantFilter, x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        modelBuilder.Entity<PermissionTemplateRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        modelBuilder.Entity<UserManagementDelegationRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        modelBuilder.Entity<IdpConfigurationRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        // AppConfiguration TenantId is nullable: global records (TenantId IS NULL) are
        // always visible regardless of tenant context; tenant-scoped records only for the
        // current tenant.
        modelBuilder.Entity<AppConfigurationRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == null ||
                x.TenantId == tenantContext.OrganizationId);

        // ParameterDefinition: catálogo global (no acotado por inquilino) con borrado LÓGICO.
        // El filtro es el único punto donde se garantiza que una definición eliminada desaparece de
        // TODAS las lecturas —incluidos los endpoints de consulta que van directos al DbContext—.
        // Los caminos de escritura que necesitan ver la fila física (unicidad de `Code`) usan
        // IgnoreQueryFilters() de forma explícita.
        modelBuilder.Entity<ParameterDefinitionRecord>()
            .HasQueryFilter(x => !x.IsDeleted);

        // ParameterTenantValue: tenant-specific parameter overrides
        modelBuilder.Entity<ParameterTenantValueRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        modelBuilder.Entity<ApprovalWorkflowRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        modelBuilder.Entity<NotificationRuleRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        modelBuilder.Entity<DocumentTypeRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        // UserDocuments have UserId but no TenantId — filtered via join in queries; no global filter here.

        modelBuilder.Entity<AccessEnforcementPolicyRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        // IGA (ADR-UMS-093): tablas acotadas por inquilino — la elegibilidad y las promociones nunca
        // cruzan fronteras de inquilino.
        modelBuilder.Entity<RoleMaturityStatusRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        modelBuilder.Entity<RolePromotionRequestRecord>()
            .HasQueryFilter(x =>
                !tenantContext.OrganizationId.HasValue ||
                x.TenantId == tenantContext.OrganizationId);

        if (Database.IsNpgsql())
        {
            // PostgreSQL has no rowversion type: bytea columns are never store-updated, so
            // IsRowVersion()'s ValueGeneratedOnAddOrUpdate leaves the column NOT NULL with no
            // value on INSERT (PostgresException 23502). Instead: pgcrypto default generates
            // the token on INSERT, and PostgresRowVersionSaveChangesInterceptor rotates it on
            // UPDATE so optimistic concurrency / If-Match (REC-10) keeps detecting conflicts.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var rowVersionProperty = entityType.FindProperty("RowVersion");
                if (rowVersionProperty != null && rowVersionProperty.ClrType == typeof(byte[]))
                {
                    rowVersionProperty.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd;
                    rowVersionProperty.SetDefaultValueSql("gen_random_bytes(8)");
                    rowVersionProperty.SetBeforeSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
                    rowVersionProperty.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Save);
                }
            }
        }

        if (Database.IsSqlServer())
        {
            // SQL Server rechaza en CREATE TABLE cualquier FK que pueda formar un ciclo de borrado
            // en cascada (error 1785), y una jerarquía autorreferenciada —un nodo de menú que
            // apunta a su padre en la misma tabla— es exactamente eso. PostgreSQL sí lo admite,
            // así que el modelo compartido las declara en cascada y aquí se degradan a NO ACTION
            // solo para este motor.
            //
            // No se pierde integridad referencial: la FK sigue existiendo y sigue impidiendo
            // referencias huérfanas. Lo que cambia es que el motor ya no borra la descendencia
            // sola. UMS borra estos agregados de forma lógica (soft delete) y reconstruye los
            // árboles desde el agregado raíz, así que el borrado físico en cascada no está en
            // el camino de ejecución.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var foreignKey in entityType.GetForeignKeys())
                {
                    var esAutorreferencia =
                        foreignKey.PrincipalEntityType == foreignKey.DeclaringEntityType;

                    if (esAutorreferencia && foreignKey.DeleteBehavior == DeleteBehavior.Cascade)
                        foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
                }

                // Los índices parciales llevan su predicado escrito a mano en las configuraciones,
                // con sintaxis PostgreSQL: identificadores entre comillas dobles y literales
                // booleanos `true`/`false`. SQL Server delimita con corchetes, representa el
                // booleano como bit y escribe la desigualdad `<>`, así que rechaza el predicado
                // con el error 10735 («cláusula WHERE incorrecta para el índice filtrado»).
                //
                // Se traduce aquí en vez de tocar los 14 puntos de llamada porque las
                // configuraciones no conocen el proveedor: se aplican con ApplyConfiguration,
                // antes de que exista conexión. El conjunto de formas es cerrado y está cubierto
                // por las pruebas de arranque: `"X" IS NOT NULL`, `"X" = true|false`, `"X" != n`.
                foreach (var index in entityType.GetIndexes())
                {
                    var filtro = index.GetFilter();
                    if (!string.IsNullOrWhiteSpace(filtro))
                        index.SetFilter(TraducirFiltroASqlServer(filtro));
                }
            }
        }

        // ADR-0076 D1: Force DateTimeKind.Utc on all DateTime properties read from the
        // database. EF Core does not preserve timezone info, so values are stored
        // as ISO-8601 strings. Without this converter, DateTime.Kind == Unspecified after
        // materialisation, which breaks serialisation and comparisons with DateTime.UtcNow.
        var utcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableUtcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue
                ? (v.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v.Value.ToUniversalTime())
                : (DateTime?)null,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(utcConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(nullableUtcConverter);
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
