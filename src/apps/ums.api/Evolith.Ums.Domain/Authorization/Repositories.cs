namespace Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Profile;
using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Authorization.Role;
using Evolith.Ums.Domain.Authorization.AssignmentRule;
using ProfileAggregate = Evolith.Ums.Domain.Authorization.Profile.Profile;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;
using PermissionTemplateAggregate = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate;
using RoleAggregate = Evolith.Ums.Domain.Authorization.Role.Role;
using AssignmentRuleAggregate = Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule;

public interface IProfileRepository : IAggregateRepository<ProfileAggregate>
{
    Task<ProfileAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProfileAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProfileAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProfileAggregate>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Perfiles ACTIVOS de un usuario en un inquilino, filtrados en la base.
    ///
    /// Es la consulta del login. Filtrar el estado en memoria traía filas que se descartaban y,
    /// sobre todo, impedía usar el índice parcial `IX_Profiles_UserId_Active`: PostgreSQL solo
    /// aprovecha un índice filtrado cuando la consulta lleva su misma condición.
    /// </summary>
    Task<IReadOnlyList<ProfileAggregate>> GetActiveByUserAndTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    // ── Dependency guard queries (lightweight count-only) ───────────────────
    /// <summary>Returns the number of active profiles assigned to a given role.</summary>
    Task<int> CountActiveByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    /// <summary>Returns the number of active profiles linked to a given template.</summary>
    Task<int> CountActiveByTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);
    /// <summary>Returns the number of active profiles owned by a given user.</summary>
    Task<int> CountActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    /// <summary>
    /// ADR-0164 §2.2: perfiles ACTIVOS acotados a una sucursal. Es la segunda mitad de la guarda de
    /// cascada del cierre definitivo —la otra son las cuentas—, y hace falta porque
    /// <c>Profiles.BranchId</c> NO tiene clave ajena contra <c>TenantBranches</c>: la base no
    /// impediría nada, así que la integridad la sostiene la aplicación o no la sostiene nadie.
    /// Solo cuenta los activos: un perfil ya desactivado no bloquea.
    /// </summary>
    Task<int> CountActiveByBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
}

public interface ISystemSuiteRepository : IAggregateRepository<SystemSuiteAggregate>
{
    Task<SystemSuiteAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SystemSuiteAggregate?> GetByCodeAsync(Code code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemSuiteAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemSuiteAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Código y nombre de varios sistemas, sin cargar el agregado.
    ///
    /// Deliberadamente NO devuelve <c>SystemSuiteAggregate</c>: cargar la suite completa cuesta
    /// siete consultas por sus módulos, nodos, acciones y recursos. Para pintar un selector de
    /// perfiles hacen falta dos cadenas.
    /// </summary>
    Task<IReadOnlyList<SystemSuiteSummary>> GetSummariesByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Una página del catálogo, filtrada, ordenada y recortada EN LA BASE.
    ///
    /// El listado cargaba todas las suites del inquilino con su árbol completo —módulos, nodos,
    /// acciones y recursos— y después filtraba, ordenaba y paginaba en memoria para devolver
    /// veinte. Con cientos de sistemas eso es traer el catálogo entero a la aplicación para
    /// tirar el 95 % (G-179).
    /// </summary>
    Task<SystemSuitePage> GetPageAsync(SystemSuitePageQuery query, CancellationToken cancellationToken = default);

    /// <summary>Agregados completos de una página, en el orden que la página fijó.</summary>
    Task<IReadOnlyList<SystemSuiteAggregate>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    // ── Guarda de cascada de la eliminación lógica (G-246) ──────────────────
    /// <summary>
    /// Cuántas referencias VIVAS —no eliminadas lógicamente— tiene el sistema, sin cargar ninguna.
    ///
    /// El conteo vive aquí —y no repartido en el repositorio de cada referente— porque «qué cuelga de
    /// un sistema» es conocimiento del sistema: cuando aparezca una tabla nueva con
    /// <c>SystemSuiteId</c>, se añade en un único sitio en vez de confiar en que alguien recuerde
    /// tocar la guarda desde el otro extremo.
    /// </summary>
    Task<SystemSuiteDependents> GetDependentsAsync(Guid id, CancellationToken cancellationToken = default);

    // NO hay Delete/Remove, ni aquí ni en IAggregateRepository: la eliminación de un sistema es un
    // cambio de estado a `SystemStatus.Deleted` que viaja por `UpdateAsync`, como cualquier otro. Que
    // el contrato no ofrezca ninguna forma de borrar la fila es la garantía —comprobable leyendo esta
    // interfaz— de que no existe una ruta de borrado físico que se pueda invocar por descuido.
}

public interface IPermissionTemplateRepository : IAggregateRepository<PermissionTemplateAggregate>
{
    Task<PermissionTemplateAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionTemplateAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionTemplateAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve las plantillas existentes para la terna (tenant, rol, suite), usada por el alta
    /// para calcular la versión siguiente y evitar la colisión del índice único (G-140).
    /// </summary>
    Task<IReadOnlyList<PermissionTemplateAggregate>> GetByTenantRoleSuiteAsync(Guid tenantId, Guid roleId, Guid systemSuiteId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // ── Dependency guard queries ────────────────────────────────────────────
    /// <summary>Returns the number of published templates for a given role.</summary>
    Task<int> CountPublishedByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    /// <summary>Returns the number of active template items that reference the given target (domain resource id).</summary>
    Task<int> CountItemsByTargetAsync(Guid targetId, CancellationToken cancellationToken = default);
}

public interface IRoleRepository : IAggregateRepository<RoleAggregate>
{
    Task<RoleAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RoleAggregate?> GetByCodeAsync(Guid systemSuiteId, Code code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleAggregate>> GetBySystemSuiteIdAsync(Guid systemSuiteId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Roles por lote. La lista de perfiles del usuario necesita, de cada uno, el nivel de
    /// jerarquía y el sistema al que pertenece su rol: sin esto habría que emitir una consulta
    /// por perfil, o cargar todos los roles del inquilino para quedarse con tres.
    /// </summary>
    Task<IReadOnlyList<RoleAggregate>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    // ── Dependency guard queries ────────────────────────────────────────────
    /// <summary>Returns the number of active child roles for a given parent role.</summary>
    Task<int> CountActiveChildRolesAsync(Guid parentRoleId, CancellationToken cancellationToken = default);
}

public interface ITemplateAssignmentRuleRepository : IAggregateRepository<AssignmentRuleAggregate>
{
    Task<AssignmentRuleAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssignmentRuleAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Returns active rules for the given tenant and role, ordered by Priority descending (highest first).</summary>
    Task<IReadOnlyList<AssignmentRuleAggregate>> GetActiveByTenantAndRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Returns whether any active rule for the given tenant already uses the given priority value.</summary>
    Task<bool> ExistsActiveWithPriorityAsync(Guid tenantId, int priority, CancellationToken cancellationToken = default);
}
