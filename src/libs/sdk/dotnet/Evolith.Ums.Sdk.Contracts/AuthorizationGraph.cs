using System.Text.Json.Serialization;

namespace Evolith.Ums.Sdk.Contracts;

/// <summary>
/// Snapshot inmutable y autocontenido del universo de autorización de un usuario
/// en el momento de autenticar.
///
/// Refleja <c>src/libs/sdk/contracts/auth-graph.schema.json</c> (v1.0.0), que
/// describe lo que emite <c>AuthGraphPayload</c> — el único mapeador del grafo,
/// común al endpoint de sistemas satélite (<c>POST /api/v1/client/authenticate</c>)
/// y al login web (<c>POST /api/v1/auth/login</c>, bajo <c>authorizationGraph</c>).
///
/// Dos convenciones del contrato:
/// <list type="number">
///   <item>Todo nombre o etiqueta viaja como <c>value</c>.</item>
///   <item>Los identificadores técnicos son OPCIONALES (<c>Guid?</c>, ausentes
///   por defecto): solo llegan si el inquilino activó
///   <c>AUTH_GRAPH_INCLUDE_TECHNICAL_METADATA</c>. La clave de negocio es
///   <c>Code</c>; no construyas lógica que dependa de un id.</item>
/// </list>
///
/// Ver ADR-UMS-088 (modelo) y ADR-0074 (versionado).
/// </summary>
public sealed record AuthorizationGraph(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("onboardingPending")] bool OnboardingPending,
    [property: JsonPropertyName("context")] PrincipalContext Context,
    [property: JsonPropertyName("authentication")] AuthenticationMetadata Authentication,
    [property: JsonPropertyName("actions")] IReadOnlyList<ActionRef> Actions,
    [property: JsonPropertyName("profiles")] IReadOnlyList<ProfileOption> Profiles,
    [property: JsonPropertyName("menuAccess")] IReadOnlyList<MenuModule> MenuAccess,
    [property: JsonPropertyName("domainPermissions")] IReadOnlyList<DomainResourcePermissions> DomainPermissions,
    [property: JsonPropertyName("featureFlags")] IReadOnlyList<FeatureFlagState> FeatureFlags,
    [property: JsonPropertyName("effectiveConfig")] EffectiveConfig EffectiveConfig,
    /// <summary>Ajustes visibles por espacio de nombres. Punto de extensión del contrato (G-178).</summary>
    [property: JsonPropertyName("settings")] IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Settings,
    [property: JsonPropertyName("scopes")] IReadOnlyList<string> Scopes,
    [property: JsonPropertyName("generatedAt")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("validUntil")] DateTimeOffset ValidUntil,
    /// <summary>
    /// v2.4.0 — estado de acceso: <c>Granted</c>, <c>NoProfileInSystem</c> u
    /// <c>OnboardingPending</c>. Convive con <see cref="OnboardingPending"/>, que se conserva para
    /// los consumidores de 2.0–2.3; el productor los deriva de la misma entrada.
    ///
    /// Se declara al final y con valor por defecto para que un consumidor que construya el record
    /// a mano contra 2.3 siga compilando. Al deserializar un grafo 2.4 llega siempre poblado.
    /// </summary>
    [property: JsonPropertyName("accessState")] string AccessState = "Granted",
    /// <summary>
    /// v2.5.0 — eco de la proyección aplicada, o <c>null</c> cuando el grafo es COMPLETO (el caso
    /// normal, y el único posible si el llamante no envía <c>?include=</c>).
    ///
    /// Es lo que permite distinguir «esta sección no la pedí» de «esta sección llegó vacía», una
    /// diferencia que ninguna otra parte del payload expresa y de la que depende que un
    /// <c>Deny</c> explícito siga contando: un <c>domainPermissions</c> vacío y uno ausente llevan
    /// al evaluador a la misma conclusión —no hay denegaciones— y solo una de las dos es cierta.
    ///
    /// Un consumidor que NECESITE una sección debe comprobar aquí que la pidió y fallar
    /// ruidosamente si no está. Continuar con un grafo incompleto no es degradar: es autorizar al
    /// azar.
    /// </summary>
    [property: JsonPropertyName("projection")] IReadOnlyList<string>? Projection = null);

/// <summary><c>SystemSuite</c>, <c>Role</c> y <c>Profile</c> son null en el grafo lobby (G-043).</summary>
public sealed record PrincipalContext(
    [property: JsonPropertyName("user")] UserSummary User,
    [property: JsonPropertyName("tenant")] TenantSummary Tenant,
    [property: JsonPropertyName("systemSuite")] SystemSuiteSummary? SystemSuite,
    [property: JsonPropertyName("role")] RoleSummary? Role,
    [property: JsonPropertyName("profile")] ProfileSummary? Profile,
    [property: JsonPropertyName("branch")] BranchSummary? Branch,
    /// <summary>
    /// v2.4.0 — eco del sistema pedido al autenticarse, o <c>null</c> si no se pidió ninguno.
    /// </summary>
    [property: JsonPropertyName("requestedSystem")] RequestedSystemRef? RequestedSystem = null);

/// <summary>v2.4.0 — referencia al sistema pedido. Solo lleva el código, que es la entrada.</summary>
public sealed record RequestedSystemRef(
    [property: JsonPropertyName("code")] string Code);

public sealed record UserSummary(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("id")] Guid? Id = null);

public sealed record TenantSummary(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("isManagementOwner")] bool IsManagementOwner,
    [property: JsonPropertyName("id")] Guid? Id = null);

public sealed record SystemSuiteSummary(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("id")] Guid? Id = null);

public sealed record RoleSummary(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("hierarchyLevel")] int HierarchyLevel,
    [property: JsonPropertyName("id")] Guid? Id = null);

public sealed record ProfileSummary(
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("id")] Guid? Id = null);

public sealed record BranchSummary(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("id")] Guid? Id = null);

public sealed record AuthenticationMetadata(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("provider")] IdpProviderRef? Provider,
    [property: JsonPropertyName("mfaRequired")] bool MfaRequired,
    [property: JsonPropertyName("issuedAt")] DateTimeOffset IssuedAt,
    [property: JsonPropertyName("sessionExpiresAt")] DateTimeOffset SessionExpiresAt);

/// <summary><c>Value</c> es la estrategia del proveedor: AzureAd, Okta, GenericOidc…</summary>
public sealed record IdpProviderRef(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("id")] Guid? Id = null);

/// <summary>
/// Un perfil al que el usuario tiene acceso: un rol dentro de un sistema, opcionalmente acotado a
/// una sucursal. Exactamente uno lleva <c>IsCurrent</c>. La lista llega ordenada por nivel de
/// jerarquía, sistema y rol: el primero es el que el servidor habría elegido por defecto.
/// </summary>
/// <param name="Id">
/// v2.4.0 — SIEMPRE presente, a diferencia del resto de ids del grafo, que siguen sujetos a
/// <c>AUTH_GRAPH_INCLUDE_TECHNICAL_METADATA</c>. Aquí no es decorativo: es la clave de
/// <c>POST /client/switch-profile</c>, la operación que esta misma lista invita a ejecutar.
/// Se mantiene al final del record —donde ya estaba— para no romper a quien construya el tipo
/// por posición, pero deja de admitir <c>null</c>.
/// </param>
public sealed record ProfileOption(
    [property: JsonPropertyName("system")] ProfileSystemRef System,
    [property: JsonPropertyName("role")] ProfileRoleRef Role,
    [property: JsonPropertyName("branch")] ProfileBranchRef? Branch,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("isCurrent")] bool IsCurrent,
    [property: JsonPropertyName("id")] Guid Id = default);

public sealed record ProfileSystemRef(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("value")] string Value);

public sealed record ProfileRoleRef(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("hierarchyLevel")] int HierarchyLevel);

public sealed record ProfileBranchRef(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("value")] string Value);

public sealed record ActionRef(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("value")] string Value);

/// <summary>
/// Módulo con su navegación alcanzable. <c>MenuAccess</c> es una lista plana de estos.
///
/// v2.0.0: el árbol es RECURSIVO. La forma anterior tenía tres niveles fijos y perdía en silencio
/// los nodos que no encajaban en ella. Recorre <c>Children</c> hasta agotarlo.
/// </summary>
public sealed record MenuModule(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("sortOrder")] int SortOrder,
    [property: JsonPropertyName("status")] string Status,
    /// <summary>v2.3.0: identificador de icono del módulo; el catálogo gráfico lo elige el cliente.</summary>
    [property: JsonPropertyName("icon")] string? Icon,
    [property: JsonPropertyName("nodes")] IReadOnlyList<NavigationNode> Nodes,
    [property: JsonPropertyName("id")] Guid? Id = null);

/// <summary>
/// Nodo del árbol de navegación. <c>Kind</c> clasifica su papel sin fijar su profundidad.
/// Solo viaja lo alcanzable: la ausencia significa «no concedido».
/// </summary>
public sealed record NavigationNode(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("sortOrder")] int SortOrder,
    /// <summary>Identificador de icono; el catálogo gráfico lo elige el cliente.</summary>
    [property: JsonPropertyName("icon")] string? Icon,
    /// <summary>Ruta relativa destino; null en los nodos que solo agrupan.</summary>
    [property: JsonPropertyName("route")] string? Route,
    [property: JsonPropertyName("actions")] IReadOnlyList<NodeAction> Actions,
    [property: JsonPropertyName("children")] IReadOnlyList<NavigationNode> Children,
    [property: JsonPropertyName("id")] Guid? Id = null);

/// <summary>
/// Acción con su efecto resuelto. <c>NotGranted</c> no viaja: es lo que significa la ausencia.
/// </summary>
public sealed record NodeAction(
    [property: JsonPropertyName("actionCode")] string ActionCode,
    [property: JsonPropertyName("effect")] AccessEffect Effect,
    [property: JsonPropertyName("source")] PermissionSource Source);

/// <summary>Recurso de dominio con su resolución por acción; lista plana, sin envoltorio.</summary>
public sealed record DomainResourcePermissions(
    [property: JsonPropertyName("resourceType")] string ResourceType,
    [property: JsonPropertyName("resourceCode")] string ResourceCode,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("actions")] IReadOnlyList<DomainActionResolution> Actions,
    [property: JsonPropertyName("resourceId")] Guid? ResourceId = null,
    [property: JsonPropertyName("moduleId")] Guid? ModuleId = null,
    [property: JsonPropertyName("parentResourceId")] Guid? ParentResourceId = null);

public sealed record DomainActionResolution(
    [property: JsonPropertyName("actionCode")] string ActionCode,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("effect")] AccessEffect Effect,
    [property: JsonPropertyName("source")] PermissionSource Source);

public sealed record FeatureFlagState(
    [property: JsonPropertyName("flagCode")] string FlagCode,
    [property: JsonPropertyName("isEnabled")] bool IsEnabled,
    [property: JsonPropertyName("matchedCriteriaType")] string? MatchedCriteriaType,
    [property: JsonPropertyName("systemSuiteId")] Guid? SystemSuiteId = null);

public sealed record EffectiveConfig(
    [property: JsonPropertyName("sessionTimeoutMinutes")] int SessionTimeoutMinutes,
    [property: JsonPropertyName("maxLoginAttempts")] int MaxLoginAttempts,
    [property: JsonPropertyName("minPasswordLength")] int MinPasswordLength,
    [property: JsonPropertyName("mfaRequiredForAdmin")] bool MfaRequiredForAdmin,
    [property: JsonPropertyName("mfaAllowedMethods")] IReadOnlyList<string> MfaAllowedMethods,
    [property: JsonPropertyName("accessTokenDurationMs")] long AccessTokenDurationMs,
    [property: JsonPropertyName("authUseExternalIdp")] bool AuthUseExternalIdp);
