import { z } from 'zod';
import { SchemaVersion } from '@domain/constants/schema-version';

// Effect and Source Enums
export enum AccessEffect {
  Allow = 'Allow',
  Deny = 'Deny',
  NotGranted = 'NotGranted',
}

export enum PermissionSource {
  Template = 'Template',
  Override = 'Override',
}

// ── Authentication ──

export const GraphIdpProviderSchema = z.object({
  id: z.string().uuid().optional(),
  code: z.string(),
  name: z.string(),
  /** Estrategia del proveedor: AzureAd, Okta, GenericOidc… */
  value: z.string(),
});

export const GraphAuthenticationSchema = z.object({
  method: z.string(),
  provider: GraphIdpProviderSchema.nullable(),
  mfaRequired: z.boolean(),
  issuedAt: z.string().datetime({ offset: true }),
  sessionExpiresAt: z.string().datetime({ offset: true }),
});

export type GraphAuthentication = z.infer<typeof GraphAuthenticationSchema>;

// ── Context ──

export const GraphUserSchema = z.object({
  id: z.string().uuid().optional(),
  email: z.string().email(),
  username: z.string(),
  value: z.string(),
  status: z.string(),
});

export const GraphTenantSchema = z.object({
  id: z.string().uuid().optional(),
  code: z.string(),
  value: z.string(),
  status: z.string(),
  isManagementOwner: z.boolean(),
});

export const GraphSystemSuiteSchema = z.object({
  id: z.string().uuid().optional(),
  code: z.string(),
  value: z.string(),
  status: z.string(),
});

export const GraphRoleSchema = z.object({
  id: z.string().uuid().optional(),
  code: z.string(),
  value: z.string(),
  hierarchyLevel: z.number().int(),
});

export const GraphProfileSchema = z.object({
  id: z.string().uuid().optional(),
  scope: z.string(),
  isActive: z.boolean(),
});

export const GraphBranchSchema = z.object({
  id: z.string().uuid().optional(),
  code: z.string(),
  value: z.string(),
});

export const GraphContextSchema = z.object({
  user: GraphUserSchema,
  tenant: GraphTenantSchema,
  // null en el grafo lobby (onboardingPending, G-043)
  systemSuite: GraphSystemSuiteSchema.nullable(),
  role: GraphRoleSchema.nullable(),
  profile: GraphProfileSchema.nullable(),
  branch: GraphBranchSchema.nullable(),
  /**
   * v2.4.0 — eco del sistema que se pidió al autenticarse, o `null` si no se pidió ninguno.
   * Opcional aquí para que un grafo 2.3 emitido por un servidor sin actualizar siga validando.
   */
  requestedSystem: z.object({ code: z.string() }).nullable().optional(),
});

export type GraphContext = z.infer<typeof GraphContextSchema>;

// ── Actions ──

/**
 * Un perfil al que el usuario tiene acceso: rol dentro de un sistema, opcionalmente acotado a una
 * sucursal. Exactamente uno lleva `isCurrent`. La lista llega ordenada por jerarquía, sistema y rol.
 */
export const GraphProfileOptionSchema = z.object({
  /**
   * v2.4.0 — el servidor lo emite SIEMPRE: es la clave del cambio de perfil, no un metadato
   * técnico. Se mantiene opcional en la validación para no rechazar un grafo 2.3, pero el código
   * que ofrezca el selector debe tratar su ausencia como «no puedo cambiar a este perfil».
   */
  id: z.string().uuid().optional(),
  system: z.object({ code: z.string(), value: z.string() }),
  role: z.object({ code: z.string(), value: z.string(), hierarchyLevel: z.number().int() }),
  branch: z.object({ code: z.string(), value: z.string() }).nullable(),
  scope: z.string(),
  isCurrent: z.boolean(),
});

export type GraphProfileOption = z.infer<typeof GraphProfileOptionSchema>;

export const GraphActionSchema = z.object({
  code: z.string(),
  value: z.string(),
});

export type GraphAction = z.infer<typeof GraphActionSchema>;

// ── Menu Access ──

/**
 * Acción resuelta sobre un nodo. `NotGranted` no viaja: es lo que significa la ausencia.
 * `Deny` sí, porque gana sobre cualquier concesión heredada y debe pintarse como bloqueado.
 */
export const GraphNodeActionSchema = z.object({
  actionCode: z.string(),
  effect: z.nativeEnum(AccessEffect),
  source: z.nativeEnum(PermissionSource),
});

/**
 * Nodo del árbol de navegación, RECURSIVO desde el contrato v2.0.0. `kind` clasifica su papel sin
 * fijar su profundidad: la forma anterior, de tres niveles, perdía los nodos que no encajaban.
 */
export interface GraphNavigationNode {
  id?: string;
  code: string;
  value: string;
  kind: string;
  sortOrder: number;
  /** Identificador de icono; el catálogo gráfico lo elige el cliente. */
  icon: string | null;
  /** Ruta relativa destino; null en los nodos que solo agrupan. */
  route: string | null;
  actions: Array<z.infer<typeof GraphNodeActionSchema>>;
  children: GraphNavigationNode[];
}

export const GraphNavigationNodeSchema: z.ZodType<GraphNavigationNode> = z.lazy(() =>
  z.object({
    id: z.string().uuid().optional(),
    code: z.string(),
    value: z.string(),
    kind: z.string(),
    sortOrder: z.number().int(),
    icon: z.string().nullable(),
    route: z.string().nullable(),
    actions: z.array(GraphNodeActionSchema),
    children: z.array(GraphNavigationNodeSchema),
  })
);

export const GraphMenuModuleSchema = z.object({
  id: z.string().uuid().optional(),
  code: z.string(),
  value: z.string(),
  sortOrder: z.number().int(),
  status: z.string(),
  /** v2.3.0: icono del módulo, mismo criterio que el del nodo (identificador, no recurso). */
  icon: z.string().nullable(),
  nodes: z.array(GraphNavigationNodeSchema),
});

export type GraphMenuModule = z.infer<typeof GraphMenuModuleSchema>;
export type GraphNodeAction = z.infer<typeof GraphNodeActionSchema>;

// ── Domain Permissions ──

export const GraphDomainActionSchema = z.object({
  actionCode: z.string(),
  value: z.string(),
  effect: z.nativeEnum(AccessEffect),
  source: z.nativeEnum(PermissionSource),
});

export const GraphDomainPermissionSchema = z.object({
  resourceId: z.string().uuid().optional(),
  resourceType: z.string(),
  resourceCode: z.string(),
  value: z.string(),
  moduleId: z.string().uuid().nullable().optional(),
  parentResourceId: z.string().uuid().nullable().optional(),
  actions: z.array(GraphDomainActionSchema),
});

export type GraphDomainPermission = z.infer<typeof GraphDomainPermissionSchema>;

// ── Feature Flags ──

export const GraphFeatureFlagSchema = z.object({
  systemSuiteId: z.string().uuid().optional(),
  flagCode: z.string(),
  isEnabled: z.boolean(),
  matchedCriteriaType: z.string().nullable(),
});

export type GraphFeatureFlag = z.infer<typeof GraphFeatureFlagSchema>;

// ── Effective Config ──

export const GraphEffectiveConfigSchema = z.object({
  sessionTimeoutMinutes: z.number().int(),
  maxLoginAttempts: z.number().int(),
  minPasswordLength: z.number().int(),
  mfaRequiredForAdmin: z.boolean(),
  mfaAllowedMethods: z.array(z.string()),
  accessTokenDurationMs: z.number().int(),
  authUseExternalIdp: z.boolean(),
});

export type GraphEffectiveConfig = z.infer<typeof GraphEffectiveConfigSchema>;

// ── Root Graph ──

export const AuthorizationGraphSchema = z.object({
  schemaVersion: z.string(),
  onboardingPending: z.boolean(),
  /**
   * v2.4.0 — estado de acceso. `NoProfileInSystem` es el caso que `onboardingPending` no sabía
   * contar: el usuario tiene perfiles, pero ninguno en el sistema pedido.
   * Opcional para que un grafo 2.3 de un servidor sin actualizar siga validando; por defecto se
   * interpreta según `onboardingPending`.
   */
  accessState: z.enum(['Granted', 'NoProfileInSystem', 'OnboardingPending']).optional(),
  context: GraphContextSchema,
  authentication: GraphAuthenticationSchema,
  actions: z.array(GraphActionSchema),
  profiles: z.array(GraphProfileOptionSchema),
  menuAccess: z.array(GraphMenuModuleSchema),
  domainPermissions: z.array(GraphDomainPermissionSchema),
  featureFlags: z.array(GraphFeatureFlagSchema),
  effectiveConfig: GraphEffectiveConfigSchema,
  /**
   * Ajustes del sistema visibles para el cliente, por espacio de nombres:
   * `settings.brand.logo_url`, `settings.ui.home_route`, `settings.locale.language`…
   * Diccionario abierto a propósito: añadir un espacio nuevo no rompe a nadie.
   */
  settings: z.record(z.string(), z.record(z.string(), z.string())),
  scopes: z.array(z.string()),
  generatedAt: z.string().datetime({ offset: true }),
  validUntil: z.string().datetime({ offset: true }),
  /**
   * v2.5.0 — eco de la proyección aplicada; ausente cuando el grafo llega COMPLETO, que es siempre
   * el caso de esta aplicación: el portal no envía `?include=`.
   *
   * Se declara igualmente por dos motivos. Uno, para que la clave no parezca desconocida el día
   * que alguien use este esquema para PARSEAR de verdad — hoy no lo hace nadie: el único consumidor
   * del fichero es su propio `z.infer` de tipos, y la app hace cast y confía. Y dos, porque es lo
   * único que distingue «esta sección no se pidió» de «esta sección llegó vacía», que es de lo que
   * depende que un `Deny` explícito siga contando.
   */
  projection: z
    .array(z.enum(['permissions', 'navigation', 'profiles', 'features', 'config', 'settings']))
    .optional(),
});

export type AuthorizationGraph = z.infer<typeof AuthorizationGraphSchema>;
