import type { AccessEffect, PermissionSource } from './access-effect.js';

/**
 * Snapshot inmutable y autocontenido del universo de autorización de un usuario
 * en el momento de autenticar.
 *
 * Refleja `src/libs/sdk/contracts/auth-graph.schema.json` (v1.0.0), que a su vez
 * describe lo que emite `AuthGraphPayload` — el único mapeador del grafo, común
 * al endpoint de sistemas satélite (`POST /api/v1/client/authenticate`) y al
 * login web (`POST /api/v1/auth/login`, bajo `authorizationGraph`).
 *
 * Dos convenciones que conviene tener presentes al leer estas interfaces:
 *   1. Todo nombre o etiqueta se expone como `value`.
 *   2. Los identificadores técnicos (`id`, `moduleId`, `systemSuiteId`…) son
 *      OPCIONALES: solo llegan si el inquilino activó
 *      `AUTH_GRAPH_INCLUDE_TECHNICAL_METADATA` (por defecto `false`). La clave
 *      de negocio es `code`; no construyas nada que dependa de un id.
 *
 * Ver ADR-UMS-088 (modelo) y ADR-0074 (versionado).
 */
export interface AuthorizationGraph {
  readonly schemaVersion: string;
  readonly onboardingPending: boolean;
  /**
   * v2.4.0 — estado de acceso, enumeración cerrada. `onboardingPending` no bastaba desde que la
   * autenticación admite filtrar por sistema: «no tiene ningún perfil» y «tiene perfiles pero
   * ninguno en el sistema pedido» son estados distintos, y tratarlos igual le mostraría un flujo
   * de alta a quien ya está dado de alta. `onboardingPending` se conserva y equivale a
   * `'OnboardingPending'`; el productor los deriva de la misma entrada, así que no divergen.
   */
  readonly accessState: 'Granted' | 'NoProfileInSystem' | 'OnboardingPending';
  readonly context: PrincipalContext;
  readonly authentication: AuthenticationMetadata;
  readonly actions: ReadonlyArray<ActionRef>;
  /** Perfiles activos del usuario; vacío en el grafo lobby. Ver {@link ProfileOption}. */
  readonly profiles: ReadonlyArray<ProfileOption>;
  readonly menuAccess: ReadonlyArray<MenuModule>;
  readonly domainPermissions: ReadonlyArray<DomainResourcePermissions>;
  readonly featureFlags: ReadonlyArray<FeatureFlagState>;
  readonly effectiveConfig: EffectiveConfig;
  /**
   * Ajustes del sistema visibles para el cliente, por espacio de nombres:
   * `settings.brand.logo_url`, `settings.ui.home_route`, `settings.locale.language`…
   * Es el punto de extensión del contrato: un espacio nuevo no cambia la forma.
   */
  readonly settings: Readonly<Record<string, Readonly<Record<string, string>>>>;
  readonly scopes: ReadonlyArray<string>;
  readonly generatedAt: string;
  readonly validUntil: string;
  /**
   * v2.5.0 — eco de la proyección aplicada, o `undefined` cuando el grafo es COMPLETO. Solo
   * aparece si el llamante envió `?include=` a `GET /client/graph`.
   *
   * Es lo único que distingue «esta sección no la pedí» de «esta sección llegó vacía». La
   * diferencia no es académica: un `domainPermissions` ausente y uno vacío llevan al evaluador a
   * la misma conclusión —no hay ninguna denegación explícita— y solo una de las dos es cierta.
   *
   * Los campos de sección de arriba se mantienen NO opcionales a propósito. Marcarlos
   * `| undefined` obligaría a cada consumidor existente a añadir guardas para un caso que no puede
   * darse en su flujo, y el compilador dejaría de avisar de las ausencias que sí importan. El
   * contrato es: si pediste una proyección, comprueba `projection` antes de leer lo que no está en
   * ella; si no pediste ninguna, está todo.
   */
  readonly projection?: ReadonlyArray<GraphProjectionToken>;
}

/**
 * v2.5.0 — vocabulario CERRADO de `?include=`, y de su eco `projection`.
 *
 * `permissions` es ATÓMICO —trae `scopes` y `domainPermissions` juntos— y no existe token para
 * pedir una sola de las dos mitades. La razón es el modo de fallo: el evaluador las combina con
 * deny-wins y lee la de dominio de forma defensiva, así que un grafo con scopes y sin
 * domainPermissions no deniega de más, CONCEDE de más, en silencio y sin traza.
 */
export type GraphProjectionToken =
  | 'permissions'
  | 'navigation'
  | 'profiles'
  | 'features'
  | 'config'
  | 'settings';

/** `systemSuite`, `role` y `profile` son null en el grafo lobby (onboardingPending). */
export interface PrincipalContext {
  readonly user: UserSummary;
  readonly tenant: TenantSummary;
  readonly systemSuite: SystemSuiteSummary | null;
  readonly role: RoleSummary | null;
  readonly profile: ProfileSummary | null;
  readonly branch: BranchSummary | null;
  /**
   * v2.4.0 — eco literal del sistema que se pidió al autenticarse, o `null` si no se pidió
   * ninguno. No es una lectura del catálogo: devuelve la entrada del propio llamante. Permite
   * rotular «no tiene acceso a X» sin llevar la configuración del cliente al navegador.
   */
  readonly requestedSystem: RequestedSystemRef | null;
}

export interface RequestedSystemRef {
  readonly code: string;
}

export interface UserSummary {
  readonly id?: string;
  readonly email: string;
  readonly username: string;
  readonly value: string;
  readonly status: 'Pending' | 'Active' | 'Blocked' | 'Deleted' | 'Denied' | string;
}

export interface TenantSummary {
  readonly id?: string;
  readonly code: string;
  readonly value: string;
  readonly status: 'Active' | 'Suspended' | 'Archived' | string;
  readonly isManagementOwner: boolean;
}

export interface SystemSuiteSummary {
  readonly id?: string;
  readonly code: string;
  readonly value: string;
  readonly status: 'Active' | 'Maintenance' | 'Deprecated' | string;
}

export interface RoleSummary {
  readonly id?: string;
  readonly code: string;
  readonly value: string;
  readonly hierarchyLevel: number;
}

export interface ProfileSummary {
  readonly id?: string;
  readonly scope: 'OrgWide' | 'BranchScoped';
  readonly isActive: boolean;
}

export interface BranchSummary {
  readonly id?: string;
  readonly code: string;
  readonly value: string;
}

export interface AuthenticationMetadata {
  readonly method: 'Local' | 'IDP' | string;
  readonly provider: IdpProviderRef | null;
  readonly mfaRequired: boolean;
  readonly issuedAt: string;
  readonly sessionExpiresAt: string;
}

export interface IdpProviderRef {
  readonly id?: string;
  readonly code: string;
  readonly name: string;
  /** Estrategia del proveedor: AzureAd, Okta, GenericOidc… */
  readonly value: string;
}

/**
 * Un perfil al que el usuario tiene acceso: un rol dentro de un sistema, opcionalmente acotado a
 * una sucursal. Exactamente uno lleva `isCurrent: true`.
 *
 * El sistema no sale del perfil —que no lo guarda— sino de su rol, que pertenece a exactamente uno.
 * La lista llega ordenada por nivel de jerarquía, sistema y rol: el primero es el que el servidor
 * habría elegido por defecto.
 */
export interface ProfileOption {
  /**
   * v2.4.0 — SIEMPRE presente, a diferencia de los demás `id` del grafo, que siguen sujetos a
   * `AUTH_GRAPH_INCLUDE_TECHNICAL_METADATA`. Aquí no es un metadato decorativo: es la clave de
   * `POST /client/switch-profile`, la operación que esta misma lista invita a ejecutar.
   */
  readonly id: string;
  readonly system: { readonly code: string; readonly value: string };
  readonly role: { readonly code: string; readonly value: string; readonly hierarchyLevel: number };
  readonly branch: { readonly code: string; readonly value: string } | null;
  readonly scope: 'OrgWide' | 'BranchScoped';
  readonly isCurrent: boolean;
}

export interface ActionRef {
  readonly code: string;
  readonly value: string;
}

/**
 * Módulo con su navegación alcanzable. `menuAccess` es una lista plana de estos.
 *
 * v2.0.0: el árbol es RECURSIVO. La forma anterior tenía tres niveles fijos
 * (menus → subMenus → options) y perdía en silencio los nodos que no encajaban en ella.
 * Recorre `children` hasta agotarlo; no presupongas una profundidad.
 */
export interface MenuModule {
  readonly id?: string;
  readonly code: string;
  readonly value: string;
  readonly sortOrder: number;
  readonly status: 'Active' | 'Inactive' | string;
  /** v2.3.0: identificador de icono del módulo, con el mismo criterio que el del nodo. */
  readonly icon: string | null;
  readonly nodes: ReadonlyArray<NavigationNode>;
}

/**
 * Nodo del árbol. `kind` clasifica su papel sin fijar su profundidad.
 *
 * Solo viaja lo ALCANZABLE: una hoja sin acciones resueltas se omite y una rama sin hojas se omite
 * con ella. La ausencia significa «no concedido».
 */
export interface NavigationNode {
  readonly id?: string;
  readonly code: string;
  readonly value: string;
  readonly kind: 'Menu' | 'SubMenu' | 'Option' | string;
  readonly sortOrder: number;
  /** Identificador de icono, no un recurso: el catálogo gráfico lo elige el cliente. */
  readonly icon: string | null;
  /** Ruta relativa destino. `null` en los nodos que solo agrupan. */
  readonly route: string | null;
  /** Vacía en los nodos de agrupación. */
  readonly actions: ReadonlyArray<NodeAction>;
  /** Siempre presente, aunque venga vacía. */
  readonly children: ReadonlyArray<NavigationNode>;
}

/**
 * Acción con su efecto resuelto. `NotGranted` no viaja: es lo que significa la ausencia. `Deny` sí,
 * porque gana sobre cualquier concesión heredada y el cliente debe pintarlo como bloqueado.
 */
export interface NodeAction {
  readonly actionCode: string;
  readonly effect: AccessEffect;
  readonly source: PermissionSource;
}

/**
 * Recurso de dominio con su resolución por acción. `domainPermissions` es una
 * lista plana de estos, sin envoltorio `resource`.
 */
export interface DomainResourcePermissions {
  readonly resourceId?: string;
  readonly resourceType: 'Aggregate' | 'Entity' | 'DomainMethod' | string;
  readonly resourceCode: string;
  readonly value: string;
  readonly moduleId?: string | null;
  readonly parentResourceId?: string | null;
  readonly actions: ReadonlyArray<DomainActionResolution>;
}

export interface DomainActionResolution {
  readonly actionCode: string;
  readonly value: string;
  readonly effect: AccessEffect;
  readonly source: PermissionSource;
}

export interface FeatureFlagState {
  readonly systemSuiteId?: string;
  readonly flagCode: string;
  readonly isEnabled: boolean;
  readonly matchedCriteriaType:
    | 'TenantId'
    | 'BranchId'
    | 'UserProfileId'
    | 'RoleCode'
    | 'Environment'
    | 'DateRange'
    | 'PercentageHash'
    | 'CustomRule'
    | null;
}

export interface EffectiveConfig {
  readonly sessionTimeoutMinutes: number;
  readonly maxLoginAttempts: number;
  readonly minPasswordLength: number;
  readonly mfaRequiredForAdmin: boolean;
  readonly mfaAllowedMethods: ReadonlyArray<string>;
  readonly accessTokenDurationMs: number;
  readonly authUseExternalIdp: boolean;
}
