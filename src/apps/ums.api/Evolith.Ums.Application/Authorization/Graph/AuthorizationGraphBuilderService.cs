using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.AppConfiguration;
using Evolith.Ums.Domain.Configuration.FeatureFlag;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Auth;
using Microsoft.Extensions.Logging;
using BranchEntity           = Evolith.Ums.Domain.Identity.Tenant.Branch.Branch;
using FeatureFlagAggregate   = Evolith.Ums.Domain.Configuration.FeatureFlag.FeatureFlag;
using MenuNodeEntity         = Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode.MenuNode;
using ProfileAggregate       = Evolith.Ums.Domain.Authorization.Profile.Profile;
using RoleAggregate          = Evolith.Ums.Domain.Authorization.Role.Role;
using SystemSuiteAggregate  = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;
using SystemSuiteSummary    = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuiteSummary;
using TenantAggregate        = Evolith.Ums.Domain.Identity.Tenant.Tenant;
using UserAccountAggregate   = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

namespace Evolith.Ums.Application.Authorization.Graph;

#pragma warning disable S125

/// <summary>
/// Builds the complete AuthorizationGraph for an authenticated user.
///
/// Cross-aggregate enrichment — identical pattern to GetProfileByIdQueryHandler.
/// Resolves: Profile → Role → SystemSuite → PermissionTemplate → Branch (optional)
/// → FeatureFlags → EffectiveConfig.
///
/// Permission resolution (deny-wins, override-takes-precedence):
///   1. Find ProfilePermission for (TargetType, TargetId, ActionId) where IsActive
///   2. IsOverride = true  → use PP.IsAllowed / PP.IsDenied   (source = Override)
///   3. IsOverride = false → use TemplateItem values           (source = Template)
///   4. IsDenied == true → Effect = Deny (always wins over Allow)
///   5. IsAllowed == true → Effect = Allow
///   6. No matching entry → Effect = NotGranted (implicit deny)
/// </summary>
public sealed class AuthorizationGraphBuilderService : IAuthorizationGraphBuilder
{
    private readonly IProfileRepository            _profileRepo;
    private readonly IRoleRepository               _roleRepo;
    private readonly ISystemSuiteRepository        _suiteRepo;
    private readonly ITenantRepository             _tenantRepo;
    private readonly IFeatureFlagRepository        _featureFlagRepo;
    private readonly IFeatureFlagEvaluator         _flagEvaluator;
    private readonly IConfigurationProvider        _configProvider;
    private readonly ILogger<AuthorizationGraphBuilderService> _logger;

    public AuthorizationGraphBuilderService(
        IProfileRepository            profileRepo,
        IRoleRepository               roleRepo,
        ISystemSuiteRepository        suiteRepo,
        ITenantRepository             tenantRepo,
        IFeatureFlagRepository        featureFlagRepo,
        IFeatureFlagEvaluator         flagEvaluator,
        IConfigurationProvider        configProvider,
        ILogger<AuthorizationGraphBuilderService> logger)
    {
        _profileRepo     = profileRepo;
        _roleRepo        = roleRepo;
        _suiteRepo       = suiteRepo;
        _tenantRepo      = tenantRepo;
        _featureFlagRepo = featureFlagRepo;
        _flagEvaluator   = flagEvaluator;
        _configProvider  = configProvider;
        _logger          = logger;
    }

    public async Task<Result<AuthorizationGraph>> BuildAsync(
        UserAccountAggregate userAccount,
        Guid                 tenantId,
        AuthMethod           authMethod,
        string?              systemCode = null,
        CancellationToken    cancellationToken = default,
        GraphProjection?     projection = null)
        => await BuildInternalAsync(userAccount, tenantId, authMethod, null, systemCode, projection, cancellationToken);

    public async Task<Result<AuthorizationGraph>> BuildForProfileAsync(
        UserAccountAggregate userAccount,
        Guid                 tenantId,
        Guid                 profileId,
        AuthMethod           authMethod,
        string?              systemCode = null,
        CancellationToken    cancellationToken = default,
        GraphProjection?     projection = null)
        // El perfil vigente NO lo elige el filtro —llega dado— pero el bloque `profiles` sí debe
        // respetarlo: sin esto, un satélite acotado a un sistema recibiria al cambiar de perfil la
        // lista de todos los sistemas en los que ese usuario trabaja (ADR-0156 §2.5).
        => await BuildInternalAsync(userAccount, tenantId, authMethod, profileId, systemCode, projection, cancellationToken);

    /// <summary>
    /// Mide la construcción y delega. La medida se toma aquí y no dentro del método para no
    /// mezclar la instrumentación con los doce pasos de armado, y para que cubra también las
    /// salidas por fallo: un grafo que tarda y falla es exactamente el que hay que ver.
    /// </summary>
    private async Task<Result<AuthorizationGraph>> BuildInternalAsync(
        UserAccountAggregate userAccount,
        Guid                 tenantId,
        AuthMethod           authMethod,
        Guid?                profileIdOverride,
        string?              systemCode,
        GraphProjection?     projection,
        CancellationToken    cancellationToken)
    {
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        var resultado = await ConstruirAsync(
            userAccount, tenantId, authMethod, profileIdOverride, systemCode, projection, cancellationToken);
        cronometro.Stop();

        // Cardinalidad acotada a propósito: sistema y rol, nunca usuario ni inquilino.
        GraphMetrics.Duracion.Record(
            cronometro.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("system", resultado.IsSuccess ? resultado.Value.Context.SystemSuite?.Code : "n/a"),
            new KeyValuePair<string, object?>("role", resultado.IsSuccess ? resultado.Value.Context.Role?.Code : "n/a"),
            new KeyValuePair<string, object?>("outcome", resultado.IsSuccess ? "ok" : "error"));

        return resultado;
    }

    private async Task<Result<AuthorizationGraph>> ConstruirAsync(
        UserAccountAggregate userAccount,
        Guid                 tenantId,
        AuthMethod           authMethod,
        Guid?                profileIdOverride,
        string?              systemCode,
        GraphProjection?     projection,
        CancellationToken    cancellationToken)
    {
        var userId = userAccount.Props.Id.GetValue();

        // Normalización única del código pedido, para que el eco del contexto y la comparación del
        // filtro no puedan divergir. Cadena vacía o en blanco == no se pidió sistema.
        var sistemaPedido = string.IsNullOrWhiteSpace(systemCode) ? null : systemCode.Trim();
        var ecoSistema    = sistemaPedido is null ? null : new GraphRequestedSystem(sistemaPedido);

        // ── 1. Tenant ─────────────────────────────────────────────────────────
        var tenant = await _tenantRepo.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return Result<AuthorizationGraph>.Failure($"Tenant {tenantId} not found.");

        // ── 2. Active profile for this user + tenant ───────────────────────────
        ProfileAggregate? profile;

        // Los perfiles activos del usuario en este inquilino se cargan SIEMPRE: alimentan el
        // bloque `profiles` del grafo, que es lo que permite al cliente ofrecer el cambio de
        // perfil sin otra llamada. La consulta ya se hacía antes; lo que no se hacía era
        // aprovecharla (G-177).
        var perfilesDelUsuario = await _profileRepo.GetActiveByUserAndTenantAsync(
            userId, tenantId, cancellationToken);

        // Un rol pertenece a exactamente un sistema, así que los roles de esos perfiles traen
        // consigo el nivel de jerarquía y el sistema. Una consulta por lote, no una por perfil.
        var rolesDeLosPerfiles = (await _roleRepo.GetByIdsAsync(
                perfilesDelUsuario.Select(p => p.Props.RoleId.GetValue()).Distinct().ToList(),
                cancellationToken))
            .ToDictionary(r => r.GetId().GetValue());

        // Los resúmenes de suite se resuelven AQUÍ y no dentro de `ConstruirPerfilesAsync`, que es
        // donde vivían: el filtro por sistema los necesita antes de elegir perfil. No añade
        // consultas — mueve una.
        var suitesPorId = (await _suiteRepo.GetSummariesByIdsAsync(
                rolesDeLosPerfiles.Values.Select(r => r.Props.SystemSuiteId.GetValue()).Distinct().ToList(),
                cancellationToken))
            .ToDictionary(x => x.Id);

        // El código de suite de cada perfil se resuelve UNA vez: lo usan el filtro y el desempate,
        // y su cálculo emite avisos cuando un perfil no resuelve. Recalcularlo dentro del
        // comparador de la ordenación repetiría esos avisos tantas veces como comparaciones haga.
        var suiteDelPerfil = perfilesDelUsuario.ToDictionary(
            p => p.GetId().GetValue(),
            p => CodigoDeSuiteDelPerfil(p, rolesDeLosPerfiles, suitesPorId));

        // ── 2.b Filtro por sistema (ADR-0156 §4) ───────────────────────────────
        //
        // El filtro se aplica SOBRE LOS PERFILES QUE EL USUARIO YA TIENE. Este camino NO consulta
        // el catálogo de sistemas por código, nunca: es lo que hace que un código inexistente y un
        // código existente sin perfil produzcan el mismo estado interno —lista vacía— y por tanto
        // la misma respuesta, sin dos ramas que puedan divergir en un mensaje, un estado o un
        // tiempo. Validar contra el catálogo y «devolver el mismo error» es la variante frágil:
        // sobrevive hasta el primer refactor que añada un log distinto en cada rama.
        var perfilesCandidatos = sistemaPedido is null
            ? perfilesDelUsuario
            : perfilesDelUsuario
                .Where(p => suiteDelPerfil[p.GetId().GetValue()] is { } codigo
                         && string.Equals(codigo, sistemaPedido, StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (profileIdOverride.HasValue)
        {
            profile = await _profileRepo.GetByIdAsync(profileIdOverride.Value, cancellationToken);
            if (profile is null)
                return Result<AuthorizationGraph>.Failure("Profile not found.");

            if (profile.Props.TenantId.GetValue() != tenantId || profile.Props.UserId.GetValue() != userId)
            {
                return Result<AuthorizationGraph>.Failure(
                    "Profile does not belong to the requested user and tenant.");
            }

            if (!profile.IsActive)
            {
                return Result<AuthorizationGraph>.Failure(
                    "Profile is inactive and cannot be previewed as an effective auth graph.");
            }
        }
        else
        {
            profile = perfilesCandidatos
                // Desempate explicable y estable: primero el rol de mayor jerarquía, luego el
                // sistema y el rol por código. Antes se ordenaba por RoleId —un GUID—, así que
                // ni el usuario ni el operador podían explicar por qué entró con ese perfil.
                //
                // G-211: el segundo criterio seguía ordenando por `SystemSuiteId`, otro GUID, pese
                // a que el comentario decía «por código». Ahora ordena de verdad por el código de
                // la suite, que el diccionario de resúmenes ya tiene resuelto: cero consultas más.
                .OrderBy(p => rolesDeLosPerfiles.TryGetValue(p.Props.RoleId.GetValue(), out var r) ? r.Props.HierarchyLevel : int.MaxValue)
                .ThenBy(p => suiteDelPerfil[p.GetId().GetValue()] ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(p => rolesDeLosPerfiles.TryGetValue(p.Props.RoleId.GetValue(), out var r) ? r.Props.Code.GetValue() : string.Empty, StringComparer.Ordinal)
                .FirstOrDefault();

            if (profile is null)
            {
                // Dos ausencias distintas, y confundirlas le mostraría a un usuario dado de alta un
                // flujo de alta (ADR-0156 §5.1):
                //
                //  · SIN NINGÚN perfil en el inquilino → grafo LOBBY (G-043). El login no falla con
                //    un 401 opaco: el cliente recibe contexto real y muestra el onboarding.
                //  · CON perfiles pero ninguno en el sistema pedido → grafo SIN ACCESO. La cuenta
                //    está de alta; lo que no tiene es acceso a ESTE sistema.
                // La proyección viaja también por estas dos salidas tempranas. Sus secciones ya
                // llegan vacías, así que no cambia lo que el cliente PUEDE hacer; cambia que el
                // eco `projection` esté presente. Sin él, un satélite que pidió una proyección y
                // cayó en el lobby recibiría un grafo sin la marca y no podría comprobar que lo
                // que tiene es lo que pidió — que es la mitad del sentido de emitirla.
                return Result<AuthorizationGraph>.Success(
                    perfilesDelUsuario.Count == 0
                        ? BuildLobbyGraph(userAccount, tenant, authMethod, ecoSistema, projection)
                        : BuildNoProfileInSystemGraph(userAccount, tenant, authMethod, ecoSistema, projection));
            }
        }

        // ── 3. Role ───────────────────────────────────────────────────────────
        var role = await _roleRepo.GetByIdAsync(profile.Props.RoleId.GetValue(), cancellationToken);
        if (role is null)
            return Result<AuthorizationGraph>.Failure("Role not found for profile.");

        // ── 4. SystemSuite (with full hierarchy) ──────────────────────────────
        var suite = await _suiteRepo.GetByIdAsync(role.Props.SystemSuiteId.GetValue(), cancellationToken);
        if (suite is null)
            return Result<AuthorizationGraph>.Failure("SystemSuite not found for role.");

// G-174: aquí se cargaban TODAS las plantillas del inquilino, con sus ítems, para
        // quedarse con una que nunca se usaba: dos consultas y la rehidratación completa
        // pagadas en cada login, cada refresco y cada preview, con un volumen que crece con
        // el tamaño del inquilino y no con el del usuario. El permMap se construye desde los
        // permisos del propio perfil (paso 8), no desde la plantilla.
        //
        // Cuando se cablee la plantilla como fuente de verdad (G-016), la consulta acotada ya
        // existe: `IPermissionTemplateRepository.GetByTenantRoleSuiteAsync`. No reintroducir
        // la carga por inquilino.

        // ── 6. Branch (if BranchScoped) ────────────────────────────────────────
        BranchEntity? branch = profile.Props.BranchId is not null
            ? tenant.Branches.FirstOrDefault(b =>
                b.Props.Id.GetValue() == profile.Props.BranchId.GetValue())
            : null;

        // ── 7. Build action lookup: ActionId → (Code, Name) ───────────────────
        var actionLookup = suite.Actions.ToDictionary(
            a => a.GetId().GetValue(),
            a => (Code: a.Props.Code.GetValue(), Name: a.Props.Name.GetValue()));

        // Segundo índice, por CÓDIGO. `BuildMenuAccess` resolvía la acción de cada opción con
        // un FirstOrDefault sobre la lista dentro de un bucle de cuatro niveles: con 2.000
        // opciones y 200 acciones son cientos de miles de comparaciones de cadena por login,
        // teniendo el diccionario ya construido cinco líneas más arriba (R-12).
        var actionByCode = suite.Actions.ToDictionary(
            a => a.Props.Code.GetValue(),
            a => a.GetId().GetValue(),
            StringComparer.Ordinal);

        // ── 8. Resolve effective permission map ────────────────────────────────
        var permMap = BuildPermissionMap(profile);

        // ── 9. Assemble graph sections ─────────────────────────────────────────
        //
        // AQUÍ es donde la proyección deja de ser un filtro de bytes. Lo que sigue declara, sección
        // por sección, qué se ahorra DE VERDAD y qué no, porque prometer un ahorro inexistente es
        // peor que no ahorrar: invita a proyectar por rendimiento donde no lo hay.
        //
        // AHORRO REAL Y AISLADO — `featureFlags`. Es la ÚNICA sección con consulta propia
        // (`GetBySystemSuiteIdForEvaluationAsync`, un split query con Include(Criteria)) más una
        // evaluación por bandera. Sin el token `features` no se consulta ni se evalúa nada.
        //
        // AHORRO REAL PERO SOLO EN MEMORIA — `actions`, `menuAccess`, `domainPermissions`,
        // `settings` y `profiles`. Saltarse su construcción evita recorrer el árbol de menús con el
        // mapa de permisos, el producto recurso×acción y la proyección de perfiles, que sobre un
        // catálogo grande no es gratis. Lo que NO evita es la consulta que los alimenta: las cuatro
        // primeras salen del MISMO agregado de suite (`_suiteRepo.GetByIdAsync`, ~5 viajes con
        // Include(Modules→Nodes→Actions)+AppSettings+Actions+DomainResources) y `profiles` sale de
        // consultas que hacen falta igualmente para ELEGIR el perfil vigente.
        //
        // POR QUÉ NO SE SALTA TAMBIÉN EL AGREGADO DE SUITE, que es la consulta cara: `context`
        // —que es sobre y no se puede proyectar— necesita `context.systemSuite.status`, y ese
        // estado solo lo tiene el agregado completo: `SystemSuiteSummary` es un
        // `record(Id, Code, Name)` sin `Status`. Saltárselo exige añadir esa columna a la
        // proyección de `GetSummariesByIdsAsync`, y eso toca el dominio y sus repositorios; queda
        // fuera de este cambio, DECLARADO y no disimulado.
        //
        // TRAMPA DE `scopes`: se DERIVAN de menuAccess + domainPermissions. Pedir `permissions` sin
        // `navigation` no evita construir el árbol de menús — solo evita serializarlo. Se construye
        // igual, y aquí se dice, en vez de dejar que alguien lo descubra midiendo.
        var necesitaMenu    = projection.LlevaNavegacion() || projection.LlevaPermisos();
        var necesitaDominio = projection.LlevaPermisos();

        IReadOnlyList<GraphAction> actions = projection.LlevaNavegacion()
            ? BuildActions(suite)
            : [];
        IReadOnlyList<GraphMenuModule> menuAccess = necesitaMenu
            ? BuildMenuAccess(suite, permMap, actionByCode)
            : [];
        IReadOnlyList<GraphDomainPermission> domainPermissions = necesitaDominio
            ? BuildDomainPermissions(suite, permMap, actionLookup)
            : [];
        IReadOnlyList<GraphFeatureFlag> featureFlags = projection.LlevaBanderas()
            ? await EvaluateFeatureFlagsAsync(suite, profile, role, branch, cancellationToken)
            : [];

        // `effectiveConfig` NO se salta nunca, aunque no se pida. No es una excepción cómoda: de él
        // salen `validUntil` (= generatedAt + sessionTimeoutMinutes) y el `sessionExpiresAt` de
        // `authentication`, que son SOBRE y viajan siempre. Y no hay nada que ahorrar: lo sirve un
        // proveedor singleton con caché en memoria, cero consultas. El token `config` decide si el
        // BLOQUE viaja, no si se calcula.
        var effectiveConfig = BuildEffectiveConfig(tenantId);
        IReadOnlyList<string> scopes = projection.LlevaPermisos()
            ? DeriveScopes(menuAccess, domainPermissions)
            : [];

        // ── 10. Context node ──────────────────────────────────────────────────
        var displayName = userAccount.Props.IdentityReference?.GetValue()
                          ?? userAccount.Props.Email.GetValue();

        var context = new GraphContext(
            User: new GraphUser(
                userId,
                userAccount.Props.Email.GetValue(),
                userAccount.Props.IdentityReference?.GetValue() ?? userAccount.Props.Email.GetValue(),
                displayName,
                userAccount.Props.Status.ToString()),
            Tenant: new GraphTenant(
                tenant.Props.Id.GetValue(),
                tenant.Props.Code.GetValue(),
                tenant.Props.Name.GetValue(),
                tenant.Props.Status.ToString(),
                tenant.IsManagementOwner),
            SystemSuite: new GraphSystemSuite(
                suite.Props.Id.GetValue(),
                suite.Props.Code.GetValue(),
                suite.Props.Name.GetValue(),
                suite.Props.Status.ToString()),
            Role: new GraphRole(
                role.Props.Id.GetValue(),
                role.Props.Code.GetValue(),
                role.Props.Value.GetValue(),
                role.Props.HierarchyLevel,
                role.Props.ParentRoleId?.GetValue()),
            Profile: new GraphProfile(profile.Props.Id.GetValue(), profile.Scope.Name, profile.IsActive),
            Branch: branch is null ? null
                : new GraphBranch(
                    branch.Props.Id.GetValue(),
                    branch.Props.Code.GetValue(),
                    branch.Props.Name.GetValue()),
            RequestedSystem: ecoSistema);

        // ── 11. Authentication node ────────────────────────────────────────────
        GraphIdpProvider? idpProvider = authMethod.Provider is not null
            ? new GraphIdpProvider(
                authMethod.Provider.GetId().GetValue(),
                authMethod.Provider.Props.Name.GetValue(),
                authMethod.Provider.Props.Code.GetValue(),
                authMethod.Provider.Props.Strategy.Name)
            : null;

        var now             = DateTime.UtcNow;
        var sessionMinutes  = effectiveConfig.SessionTimeoutMinutes;
        var authentication  = new GraphAuthentication(
            Method:           authMethod.Type.ToString(),
            Provider:         idpProvider,
            MfaRequired:      effectiveConfig.MfaRequiredForAdmin,
            IssuedAt:         now,
            SessionExpiresAt: now.AddMinutes(sessionMinutes));

        // Se proyectan los CANDIDATOS, no todos los perfiles del usuario. Es una regla de
        // seguridad, no de estética: con el grafo acotado a un sistema, mandar la lista completa
        // le entregaría a ese satélite el inventario de los demás sistemas en los que el usuario
        // trabaja — información del inquilino filtrándose a quien no la necesita (ADR-0156 §2.5).
        IReadOnlyList<GraphProfileOption> perfiles = projection.LlevaPerfiles()
            ? ConstruirPerfiles(
                perfilesCandidatos, rolesDeLosPerfiles, suitesPorId, tenant, profile.GetId().GetValue())
            : [];

        var graph = AuthorizationGraph.Build(
            context, authentication, actions, menuAccess,
            domainPermissions, featureFlags, effectiveConfig, scopes, now,
            profiles: perfiles,
            settings: projection.LlevaAjustes()
                ? ConstruirAjustes(suite)
                : new Dictionary<string, IReadOnlyDictionary<string, string>>(),
            projection: projection);

        return Result<AuthorizationGraph>.Success(graph);
    }

    // ── Permission Resolution ────────────────────────────────────────────────

    /// <summary>
    /// Builds (TargetId, ActionId) → (Effect, Source) map from profile permissions.
    /// Deny-wins: if any permission for a key is Deny, it stays Deny.
    /// </summary>
    private static Dictionary<(Guid TargetId, Guid ActionId), (AccessEffect Effect, PermissionSource Source)>
        BuildPermissionMap(
            ProfileAggregate profile)
    {
        var map = new Dictionary<(Guid TargetId, Guid ActionId), (AccessEffect Effect, PermissionSource Source)>();

        foreach (var pp in profile.Permissions.Where(p => p.Props.IsActive))
        {
            var key = (pp.Props.TargetId.GetValue(), pp.Props.ActionId.GetValue());

            var effect = ResolveEffect(pp.Props.IsAllowed, pp.Props.IsDenied);
            if (effect == AccessEffect.NotGranted) continue;

            var source = pp.Props.IsOverride ? PermissionSource.Override : PermissionSource.Template;

            if (map.TryGetValue(key, out var existing))
            {
                // Deny always wins
                if (existing.Effect == AccessEffect.Deny) continue;
                if (effect == AccessEffect.Deny) { map[key] = (effect, source); continue; }
                // Override wins over Template for Allow
                if (source == PermissionSource.Override) map[key] = (effect, source);
            }
            else
            {
                map[key] = (effect, source);
            }
        }

        return map;
    }

    private static AccessEffect ResolveEffect(bool isAllowed, bool isDenied)
    {
        if (isDenied)  return AccessEffect.Deny;
        if (isAllowed) return AccessEffect.Allow;
        return AccessEffect.NotGranted;
    }

    // ── Section Builders ─────────────────────────────────────────────────────

    /// <summary>
    /// Código de la suite a la que pertenece el perfil, o <c>null</c> si su rol o su suite no
    /// resuelven. El sistema NO sale del perfil —que no lo guarda— sino de su rol, que pertenece a
    /// exactamente uno.
    /// </summary>
    private string? CodigoDeSuiteDelPerfil(
        ProfileAggregate perfil,
        Dictionary<Guid, RoleAggregate> rolesPorId,
        Dictionary<Guid, SystemSuiteSummary> suitesPorId)
    {
        if (!rolesPorId.TryGetValue(perfil.Props.RoleId.GetValue(), out var rol))
        {
            // Con el filtro por sistema activo este descarte cambia de significado: deja de ser
            // una fila que no se pinta y pasa a poder ser la diferencia entre «tienes acceso» y
            // «no tienes acceso» (ADR-0156 §4.3). Callarlo dejaría al operador sin forma de
            // explicar un `NoProfileInSystem` que no debería serlo.
            _logger.LogWarning(
                "Perfil {PerfilId} descartado del grafo: su rol {RolId} no resuelve.",
                perfil.GetId().GetValue(), perfil.Props.RoleId.GetValue());
            return null;
        }

        if (!suitesPorId.TryGetValue(rol.Props.SystemSuiteId.GetValue(), out var suite))
        {
            _logger.LogWarning(
                "Perfil {PerfilId} descartado del grafo: la suite {SuiteId} de su rol {RolId} no resuelve.",
                perfil.GetId().GetValue(), rol.Props.SystemSuiteId.GetValue(), rol.GetId().GetValue());
            return null;
        }

        return suite.Code;
    }

    /// <summary>
    /// Proyecta los perfiles candidatos del usuario para el selector del cliente.
    ///
    /// El sistema se resuelve por el rol —el perfil no lo guarda— desde los resúmenes de suite que
    /// el llamante ya resolvió: cargar el agregado de cada suite costaría siete consultas por
    /// suite para obtener dos cadenas. La sucursal sale del inquilino, que ya viene con sus
    /// sucursales cargadas, así que no añade ninguna consulta.
    /// </summary>
    private static IReadOnlyList<GraphProfileOption> ConstruirPerfiles(
        IReadOnlyList<ProfileAggregate> perfiles,
        Dictionary<Guid, RoleAggregate> rolesPorId,
        Dictionary<Guid, SystemSuiteSummary> suitesPorId,
        TenantAggregate tenant,
        Guid perfilVigenteId)
    {
        if (perfiles.Count == 0) return [];

        var opciones = new List<GraphProfileOption>(perfiles.Count);

        foreach (var perfil in perfiles)
        {
            if (!rolesPorId.TryGetValue(perfil.Props.RoleId.GetValue(), out var rol)) continue;
            if (!suitesPorId.TryGetValue(rol.Props.SystemSuiteId.GetValue(), out var suite)) continue;

            var sucursal = perfil.Props.BranchId is null
                ? null
                : tenant.Branches.FirstOrDefault(b => b.Props.Id.GetValue() == perfil.Props.BranchId.GetValue());

            opciones.Add(new GraphProfileOption(
                Id:             perfil.GetId().GetValue(),
                SystemCode:     suite.Code,
                SystemName:     suite.Name,
                RoleCode:       rol.Props.Code.GetValue(),
                RoleName:       rol.Props.Value.GetValue(),
                HierarchyLevel: rol.Props.HierarchyLevel,
                BranchCode:     sucursal?.Props.Code.GetValue(),
                BranchName:     sucursal?.Props.Name.GetValue(),
                Scope:          perfil.Scope.Name,
                IsCurrent:      perfil.GetId().GetValue() == perfilVigenteId));
        }

        // Mismo orden que el desempate del login: lo que el cliente pinta primero es lo que el
        // servidor habría elegido.
        return opciones
            .OrderBy(o => o.HierarchyLevel)
            .ThenBy(o => o.SystemCode, StringComparer.Ordinal)
            .ThenBy(o => o.RoleCode, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Ajustes del sistema visibles para el cliente, agrupados por espacio de nombres.
    ///
    /// Convención: la clave es `ESPACIO_RESTO` y se proyecta como `settings["espacio"]["resto"]`,
    /// ambos en minúsculas. `BRAND_LOGO_URL` viaja como `settings.brand.logo_url`. Es una regla
    /// mecánica y reversible a propósito: cualquier taxonomía cerrada en el código habría que
    /// tocarla cada vez que el negocio quiera publicar algo nuevo, y el objetivo es justo lo
    /// contrario — que añadir un ajuste no toque el contrato.
    ///
    /// Solo se proyecta lo marcado con `IsClientVisible` (G-178). Una clave sin espacio de nombres
    /// —sin guion bajo— cae en `general`.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ConstruirAjustes(
        SystemSuiteAggregate suite)
    {
        var grupos = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var ajuste in suite.AppSettings.Where(a => a.IsClientVisible))
        {
            var clave = ajuste.Key.GetValue();
            var corte = clave.IndexOf('_');

            var espacio = corte <= 0 ? "general" : clave[..corte].ToLowerInvariant();
            var resto   = corte <= 0 ? clave.ToLowerInvariant() : clave[(corte + 1)..].ToLowerInvariant();

            if (!grupos.TryGetValue(espacio, out var grupo))
            {
                grupo = new Dictionary<string, string>(StringComparer.Ordinal);
                grupos[espacio] = grupo;
            }

            grupo[resto] = ajuste.Value.GetValue();
        }

        return grupos.ToDictionary(
            g => g.Key,
            g => (IReadOnlyDictionary<string, string>)g.Value,
            StringComparer.Ordinal);
    }


    private static IReadOnlyList<GraphAction> BuildActions(SystemSuiteAggregate suite)
        => suite.Actions
            .Select(a => new GraphAction(
                a.GetId().GetValue(),
                a.Props.Code.GetValue(),
                a.Props.Name.GetValue()))
            .OrderBy(a => a.Code)
            .ToList();

    /// <summary>
    /// Proyecta la navegación ALCANZABLE de cada módulo, recorriendo el árbol de nodos a
    /// cualquier profundidad (ADR-0090).
    ///
    /// Antes recorría exactamente tres niveles literales —Menu, luego SubMenu, luego Option— y
    /// cualquier nodo fuera de ese patrón desaparecía sin dejar traza (G-171). Y emitía TODAS las
    /// filas, incluidas las `NotGranted`: para un perfil restringido, más de la mitad del payload
    /// era decir que no.
    ///
    /// Regla de poda, coherente con el fail-closed que ya regía: una hoja sin acciones concedidas
    /// ni denegadas explícitamente no viaja; una rama que se queda sin hojas tampoco; un módulo
    /// sin nodos, tampoco. Ausencia = no concedido.
    /// </summary>
    private static IReadOnlyList<GraphMenuModule> BuildMenuAccess(
        SystemSuiteAggregate suite,
        Dictionary<(Guid, Guid), (AccessEffect Effect, PermissionSource Source)> permMap,
        Dictionary<string, Guid> actionByCode)
    {
        var modules = new List<GraphMenuModule>();

        foreach (var module in suite.Modules.OrderBy(m => m.Props.SortOrder))
        {
            var nodos = module.Nodes
                .OrderBy(n => n.SortOrder)
                .Select(n => ProyectarNodo(n, permMap, actionByCode))
                .Where(n => n is not null)
                .Select(n => n!)
                .ToList();

            if (nodos.Count == 0) continue;

            modules.Add(new GraphMenuModule(
                module.Props.Id.GetValue(),
                module.Props.Code.GetValue(),
                module.Props.Name.GetValue(),
                module.Props.SortOrder,
                module.Props.Status?.Name ?? "Active",
                module.Props.Icon,
                nodos));
        }

        return modules;
    }

    /// <summary>
    /// Proyecta un nodo y su descendencia. Devuelve null si nada de ese subárbol es alcanzable.
    /// </summary>
    private static GraphNavigationNode? ProyectarNodo(
        MenuNodeEntity nodo,
        Dictionary<(Guid, Guid), (AccessEffect Effect, PermissionSource Source)> permMap,
        Dictionary<string, Guid> actionByCode)
    {
        var nodeId = nodo.GetId().GetValue();

        // N:M (ADR-0090): una opción puede vincular varias acciones. Se conserva una entrada por
        // acción con efecto explícito; las que no se conceden ni se deniegan, se omiten.
        var acciones = nodo.ActionCodes
            .Select(a => a.GetValue())
            .Select(actionCode =>
            {
                if (!actionByCode.TryGetValue(actionCode, out var actionId)) return null;
                if (!permMap.TryGetValue((nodeId, actionId), out var perm)) return null;
                if (perm.Effect == AccessEffect.NotGranted) return null;

                return new GraphNodeAction(actionCode, perm.Effect, perm.Source);
            })
            .Where(a => a is not null)
            .Select(a => a!)
            .ToList();

        var hijos = nodo.Children
            .OrderBy(n => n.SortOrder)
            .Select(n => ProyectarNodo(n, permMap, actionByCode))
            .Where(n => n is not null)
            .Select(n => n!)
            .ToList();

        // Ni acciones propias ni descendencia alcanzable: el nodo no existe para este perfil.
        if (acciones.Count == 0 && hijos.Count == 0) return null;

        return new GraphNavigationNode(
            nodeId,
            nodo.Code.GetValue(),
            nodo.Label.GetValue(),
            nodo.Kind.ToString(),
            nodo.SortOrder,
            nodo.Props.Presentation.Icon,
            nodo.Props.Presentation.Route,
            acciones,
            hijos);
    }

    /// <summary>
    /// Recursos de dominio con sus acciones RESUELTAS.
    ///
    /// Antes emitía el producto cartesiano completo —cada recurso por cada acción del sistema—
    /// aunque la inmensa mayoría dijera `NotGranted`: sobre datos reales, 286 filas de las que un
    /// perfil restringido usaba 22, y más de la mitad del payload del login. Ahora solo viajan las
    /// acciones concedidas o denegadas explícitamente, y un recurso sin ninguna no viaja. La
    /// ausencia sigue significando denegación (fail-closed, G-039): no se pierde información, se
    /// deja de repetir en cada login lo que el contrato dice una vez.
    /// </summary>
    private static IReadOnlyList<GraphDomainPermission> BuildDomainPermissions(
        SystemSuiteAggregate suite,
        Dictionary<(Guid, Guid), (AccessEffect Effect, PermissionSource Source)> permMap,
        Dictionary<Guid, (string Code, string Name)> actionLookup)
    {
        var result = new List<GraphDomainPermission>();
        var accionesOrdenadas = actionLookup.OrderBy(kv => kv.Value.Code).ToList();

        foreach (var resource in suite.DomainResources.OrderBy(r => r.Props.Code.GetValue()))
        {
            var domainActions = new List<GraphDomainAction>();

            foreach (var (actionId, (actionCode, actionName)) in accionesOrdenadas)
            {
                if (!permMap.TryGetValue((resource.Props.Id.GetValue(), actionId), out var perm)) continue;
                if (perm.Effect == AccessEffect.NotGranted) continue;

                domainActions.Add(new GraphDomainAction(
                    actionId, actionCode, actionName, perm.Effect, perm.Source));
            }

            if (domainActions.Count == 0) continue;

            result.Add(new GraphDomainPermission(
                resource.Props.Id.GetValue(),
                resource.Props.Type.Name,
                resource.Props.Code.GetValue(),
                resource.Props.Name.GetValue(),
                resource.Props.ModuleId?.GetValue(),
                resource.Props.ParentResourceId?.GetValue(),
                domainActions));
        }

        return result;
    }

    private async Task<IReadOnlyList<GraphFeatureFlag>> EvaluateFeatureFlagsAsync(
        SystemSuiteAggregate suite,
        ProfileAggregate     profile,
        RoleAggregate        role,
        BranchEntity?        branch,
        CancellationToken    cancellationToken)
    {
        var flags = await _featureFlagRepo.GetBySystemSuiteIdForEvaluationAsync(
            suite.Props.Id.GetValue(), cancellationToken);

        var ctx = new EvaluationContext(
            TenantId:  profile.Props.TenantId.GetValue(),
            BranchId:  branch?.Props.Id.GetValue(),
            ProfileId: profile.Props.Id.GetValue(),
            RoleCode:  role.Props.Code.GetValue(),
            Environment: "Production");

        return flags
            .Where(f => f.Props.Status == FlagStatus.Active)
            .Select(f =>
            {
                var r = _flagEvaluator.Evaluate(f, ctx);
                return new GraphFeatureFlag(
                    f.Props.FlagCode,
                    f.Props.SystemSuiteId.GetValue(),
                    r.IsEnabled,
                    r.MatchedCriteriaType);
            })
            .ToList();
    }

    /// <summary>
    /// G-043 — construye el GRAFO LOBBY para un usuario autenticado y aprobado que aún no tiene perfil
    /// activo (onboarding pendiente). Trae Context de usuario+inquilino reales pero sin SystemSuite/Role/
    /// Profile, con Actions/MenuAccess/DomainPermissions/FeatureFlags/Scopes vacíos y OnboardingPending=true.
    /// Así el login NO falla (evita el 401 AUTH_000 opaco) y el cliente puede mostrar el onboarding.
    /// </summary>
    private AuthorizationGraph BuildLobbyGraph(
        UserAccountAggregate userAccount, TenantAggregate tenant, AuthMethod authMethod,
        GraphRequestedSystem? ecoSistema = null, GraphProjection? projection = null)
    {
        var userId          = userAccount.Props.Id.GetValue();
        var email           = userAccount.Props.Email.GetValue();
        var effectiveConfig = BuildEffectiveConfig(tenant.Props.Id.GetValue());
        var now             = DateTime.UtcNow;

        GraphIdpProvider? idpProvider = authMethod.Provider is not null
            ? new GraphIdpProvider(
                authMethod.Provider.GetId().GetValue(),
                authMethod.Provider.Props.Name.GetValue(),
                authMethod.Provider.Props.Code.GetValue(),
                authMethod.Provider.Props.Strategy.Name)
            : null;

        var context = new GraphContext(
            User: new GraphUser(
                userId, email,
                userAccount.Props.IdentityReference?.GetValue() ?? email,
                email,
                userAccount.Props.Status.ToString()),
            Tenant: new GraphTenant(
                tenant.Props.Id.GetValue(),
                tenant.Props.Code.GetValue(),
                tenant.Props.Name.GetValue(),
                tenant.Props.Status.ToString(),
                tenant.IsManagementOwner),
            SystemSuite: null, Role: null, Profile: null, Branch: null,
            RequestedSystem: ecoSistema);

        var authentication = new GraphAuthentication(
            Method:           authMethod.Type.ToString(),
            Provider:         idpProvider,
            MfaRequired:      effectiveConfig.MfaRequiredForAdmin,
            IssuedAt:         now,
            SessionExpiresAt: now.AddMinutes(effectiveConfig.SessionTimeoutMinutes));

        return AuthorizationGraph.Build(
            context, authentication,
            Array.Empty<GraphAction>(),
            Array.Empty<GraphMenuModule>(),
            Array.Empty<GraphDomainPermission>(),
            Array.Empty<GraphFeatureFlag>(),
            effectiveConfig,
            Array.Empty<string>(),
            now,
            onboardingPending: true,
            projection: projection);
    }

    /// <summary>
    /// ADR-0156 §5.1 — grafo SIN ACCESO: el usuario tiene perfiles activos en el inquilino, pero
    /// ninguno en el sistema que se pidió. O el sistema pedido no existe, que es el mismo estado
    /// interno y por tanto la misma respuesta (§4.1).
    ///
    /// Se parece al lobby en la forma —contexto real, secciones vacías— y se diferencia en lo
    /// único que importa: <c>OnboardingPending = false</c>. La cuenta está dada de alta; lo que no
    /// tiene es acceso a ESTE sistema. Enseñarle un flujo de alta sería mentirle.
    ///
    /// El token se emite igualmente (§5.4): la identidad quedó probada y el satélite necesita un
    /// portador para volver a consultar <c>GET /client/graph</c> más tarde — un administrador
    /// puede asignarle el perfil sin que el usuario vuelva a teclear su contraseña.
    /// </summary>
    private AuthorizationGraph BuildNoProfileInSystemGraph(
        UserAccountAggregate userAccount, TenantAggregate tenant, AuthMethod authMethod,
        GraphRequestedSystem? ecoSistema, GraphProjection? projection = null)
    {
        var effectiveConfig = BuildEffectiveConfig(tenant.Props.Id.GetValue());
        var now             = DateTime.UtcNow;
        var email           = userAccount.Props.Email.GetValue();

        GraphIdpProvider? idpProvider = authMethod.Provider is not null
            ? new GraphIdpProvider(
                authMethod.Provider.GetId().GetValue(),
                authMethod.Provider.Props.Name.GetValue(),
                authMethod.Provider.Props.Code.GetValue(),
                authMethod.Provider.Props.Strategy.Name)
            : null;

        var context = new GraphContext(
            User: new GraphUser(
                userAccount.Props.Id.GetValue(), email,
                userAccount.Props.IdentityReference?.GetValue() ?? email,
                email,
                userAccount.Props.Status.ToString()),
            Tenant: new GraphTenant(
                tenant.Props.Id.GetValue(),
                tenant.Props.Code.GetValue(),
                tenant.Props.Name.GetValue(),
                tenant.Props.Status.ToString(),
                tenant.IsManagementOwner),
            SystemSuite: null, Role: null, Profile: null, Branch: null,
            RequestedSystem: ecoSistema);

        var authentication = new GraphAuthentication(
            Method:           authMethod.Type.ToString(),
            Provider:         idpProvider,
            MfaRequired:      effectiveConfig.MfaRequiredForAdmin,
            IssuedAt:         now,
            SessionExpiresAt: now.AddMinutes(effectiveConfig.SessionTimeoutMinutes));

        return AuthorizationGraph.Build(
            context, authentication,
            Array.Empty<GraphAction>(),
            Array.Empty<GraphMenuModule>(),
            Array.Empty<GraphDomainPermission>(),
            Array.Empty<GraphFeatureFlag>(),
            effectiveConfig,
            Array.Empty<string>(),
            now,
            onboardingPending: false,
            // `profiles` vacío a propósito: el usuario TIENE perfiles, pero ninguno de este
            // sistema, y enumerárselos aquí sería entregarle a un satélite el inventario de los
            // demás sistemas del inquilino — exactamente lo que el filtro evita (§2.5).
            profiles: [],
            accessState: GraphAccessState.NoProfileInSystem,
            projection: projection);
    }

    private GraphEffectiveConfig BuildEffectiveConfig(Guid tenantId)
        => new(
            SessionTimeoutMinutes: _configProvider.GetValueAs<int>(AppConfigurationCodes.SessionTimeoutMinutes, tenantId, AppConfigurationDefaults.SessionTimeoutMinutes),
            MaxLoginAttempts:      _configProvider.GetValueAs<int>(AppConfigurationCodes.MaxLoginAttempts, tenantId, AppConfigurationDefaults.MaxLoginAttempts),
            MinPasswordLength:     _configProvider.GetValueAs<int>(AppConfigurationCodes.MinPasswordLength, tenantId, AppConfigurationDefaults.MinPasswordLength),
            MfaRequiredForAdmin:   _configProvider.GetValueAs<bool>(AppConfigurationCodes.MfaRequiredForAdmin, tenantId, AppConfigurationDefaults.MfaRequiredForAdmin),
            MfaAllowedMethods:     _configProvider.ForTenant(tenantId).MfaAllowedMethods.Select(method => method.Name).ToArray(),
            AccessTokenDurationMs: _configProvider.GetValueAs<int>(AppConfigurationCodes.AccessTokenDurationMs, tenantId, AppConfigurationDefaults.AccessTokenDurationMs),
            AuthUseExternalIdp:    _configProvider.GetValueAs<bool>(AppConfigurationCodes.AuthUseExternalIdp, tenantId, AppConfigurationDefaults.AuthUseExternalIdp));

    private static IReadOnlyList<string> DeriveScopes(
        IReadOnlyList<GraphMenuModule>       menuAccess,
        IReadOnlyList<GraphDomainPermission> domainPerms)
    {
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (nodo, accion) in menuAccess
                     .SelectMany(module => RecorrerNodos(module.Nodes))
                     .SelectMany(n => n.Actions.Where(a => a.Effect == AccessEffect.Allow), (n, a) => (n, a)))
            scopes.Add($"{nodo.Code.ToLowerInvariant()}.{accion.ActionCode.ToLowerInvariant()}");

        foreach (var (res, act) in domainPerms
                     .SelectMany(res => res.Actions.Where(a => a.Effect == AccessEffect.Allow),
                                 (res, act) => (res, act)))
            scopes.Add($"{res.ResourceCode.ToLowerInvariant()}.{act.ActionCode.ToLowerInvariant()}");

        return scopes.OrderBy(s => s).ToList();
    }

    /// <summary>Aplana el árbol de navegación en profundidad, sin presuponer niveles.</summary>
    private static IEnumerable<GraphNavigationNode> RecorrerNodos(IEnumerable<GraphNavigationNode> nodos)
    {
        foreach (var nodo in nodos)
        {
            yield return nodo;
            foreach (var hijo in RecorrerNodos(nodo.Children)) yield return hijo;
        }
    }
}
