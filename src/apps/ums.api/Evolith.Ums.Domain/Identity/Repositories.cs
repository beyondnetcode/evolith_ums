namespace Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Identity.Tenant.Branch;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Identity.UserManagementDelegation;
using TenantAggregate = Evolith.Ums.Domain.Identity.Tenant.Tenant;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;
using UserManagementDelegationAggregate = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation;

public interface ITenantRepository : IAggregateRepository<TenantAggregate>
{
    Task<TenantAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TenantAggregate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// G-161: ¿existe ya una sucursal con este código bajo el inquilino? Consulta autoritativa que
    /// IGNORA el filtro global por inquilino, para que la verificación de unicidad sea correcta aun
    /// cuando el actor (admin interno) opera sobre OTRO inquilino distinto a su contexto — donde el
    /// filtro global vaciaría la colección `Branches` del agregado y la guarda en memoria de
    /// `Tenant.AddBranch` no vería el duplicado. Es de solo lectura y se usa en la vía de escritura.
    /// </summary>
    Task<bool> BranchCodeExistsAsync(Guid tenantId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// ADR-0164: BITÁCORA de una sucursal — sus episodios (apertura, desactivación, reactivación,
    /// cierre definitivo) en orden cronológico, con fecha, autor y la foto de cómo era la sucursal
    /// en cada uno.
    ///
    /// <para>Es una lectura APARTE y no una colección del agregado a propósito: ninguna invariante
    /// depende de la historia, y cargarla con cada <c>GetByIdAsync</c> —ruta caliente, la usa hasta
    /// el grafo de autorización— traería filas que nadie mira. Devuelve la bitácora aunque la
    /// sucursal esté cerrada: preguntar por el pasado de algo cerrado es justamente el caso de uso.</para>
    /// </summary>
    Task<IReadOnlyList<BranchLifecycleEntry>> GetBranchLifecycleAsync(
        Guid tenantId, Guid branchId, CancellationToken cancellationToken = default);
    /// <summary>
    /// REC-12: Server-side paginated query. SQL implementations use Skip/Take at the DB level.
    /// InMemory implementations call GetAllAsync then apply in-memory pagination.
    /// Returns (items in the current page, total matching items).
    /// When tenantId is provided, only returns the matching tenant and its direct children (ParentTenantId == tenantId).
    /// Null tenantId means cross-tenant access (internal admins only).
    /// </summary>
    /// <param name="searchField">
    /// Campo sobre el que aplica <paramref name="search"/> ("code" | "name"). Es independiente de
    /// <paramref name="sortBy"/> (que solo ordena). Si es nulo/vacío se usa <paramref name="sortBy"/>
    /// por compatibilidad. Antes el campo de búsqueda se derivaba de sortBy, ignorando el parámetro
    /// `criteria` del API (búsqueda por código imposible sin ordenar por código) — G-014 residual.
    /// </param>
    Task<(IReadOnlyList<TenantAggregate> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, string? status, string sortBy, string sortOrder,
        Guid? tenantId = null, CancellationToken cancellationToken = default, string? searchField = null);

    /// <summary>
    /// REC-16: Soft-delete a tenant by ID. Marks IsDeleted=true and records who deleted it.
    /// Returns false if the tenant was not found.
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, string deletedBy, CancellationToken cancellationToken = default);
}

public interface IUserAccountRepository : IAggregateRepository<UserAccountAggregate>
{
    Task<UserAccountAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserAccountAggregate?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<UserAccountAggregate?> GetByTenantAndEmailAsync(Guid tenantId, Email email, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAccountAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAccountAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// ¿Existe ALGUNA cuenta de usuario en toda la plataforma? Es la prueba de «primer arranque»
    /// del patrón First-Run: solo con la tabla vacía puede el arranque en frío crear el
    /// administrador inicial.
    ///
    /// <para>Sin ámbito de inquilino y SIN filtros de consulta, a propósito y en ambos sentidos.
    /// El filtro global de <c>UserAccountRecord</c> combina borrado lógico e inquilino en contexto,
    /// de modo que una base cuyas cuentas estén todas soft-borradas se vería <b>vacía</b> — y el
    /// arranque volvería a crear un administrador con la credencial del entorno, que es
    /// exactamente la puerta que este patrón existe para cerrar. La pregunta que hay que
    /// responder no es «¿hay cuentas visibles?» sino «¿se inicializó esta plataforma alguna
    /// vez?».</para>
    /// </summary>
    Task<bool> AnyUserAccountExistsAsync(CancellationToken cancellationToken = default);

    // ── Dependency guard queries ────────────────────────────────────────────
    /// <summary>Returns the number of non-deleted users in the given tenant.</summary>
    Task<int> CountActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    /// <summary>G-046: Returns the number of active users bound to the given branch. Used as a
    /// dependency guard so a branch with active accounts cannot be deactivated/removed and leave
    /// dangling branch references.</summary>
    Task<int> CountActiveByBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    /// <summary>
    /// REC-12: Server-side paginated query. SQL implementations use Skip/Take at the DB level.
    /// </summary>
    Task<(IReadOnlyList<UserAccountAggregate> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, string? status, string sortBy, string sortOrder,
        Guid? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// REC-16: Soft-delete a user account by ID. For SQL repos: marks IsDeleted=true and
    /// anonymizes PII (email → gdpr_del_{sha256}@anonymized.invalid, IdentityReference → null).
    /// For InMemory repos: removes the record from the store.
    /// Returns false if not found.
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, string deletedBy, CancellationToken cancellationToken = default);
}

public interface IUserManagementDelegationRepository : IAggregateRepository<UserManagementDelegationAggregate>
{
    Task<UserManagementDelegationAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserManagementDelegationAggregate>> GetByDelegatedAdminAsync(Guid delegatedAdminId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserManagementDelegationAggregate>> GetByDelegatingAdminAsync(Guid delegatingAdminId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserManagementDelegationAggregate>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserManagementDelegationAggregate>> GetExpiredActiveAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserManagementDelegationAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
}
