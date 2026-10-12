/**
 * system-suite.schema.ts — Runtime validation schemas (Zod)
 *
 * Single source of truth for SystemSuite types. Validates API responses
 * at the infrastructure boundary so TypeScript types match reality at runtime.
 *
 * Backend enums:
 *   SystemStatus: Active | Maintenance | Deprecated
 */
import { z } from 'zod';

export const SystemStatusSchema = z.enum(['Active', 'Maintenance', 'Deprecated']);

const GuidSchema = z
  .string()
  .regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/);

export const SystemSuiteActionSchema = z.object({
  id: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  code: z.string(),
  name: z.string(),
});

// ── Árbol de nodos recursivo (ADR-0090) ──
export const SystemSuiteNodeMetadataSchema = z.object({
  responsable: z.string().nullable().optional(),
  criticidad: z.string().nullable().optional(),
  productoImpactado: z.string().nullable().optional(),
  componenteTecnico: z.string().nullable().optional(),
  dependencias: z.string().nullable().optional(),
  evidencias: z.string().nullable().optional(),
  trazabilidadSdlc: z.string().nullable().optional(),
});

export type SystemSuiteNodeMetadata = z.infer<typeof SystemSuiteNodeMetadataSchema>;

export type SystemSuiteNode = {
  id: string;
  parentNodeId: string | null;
  kind: string;
  code: string;
  label: string;
  description: string;
  status: string;
  sortOrder: number;
  actionCodes: string[];
  metadata: SystemSuiteNodeMetadata | null;
  children: SystemSuiteNode[];
};

export const SystemSuiteNodeSchema: z.ZodType<SystemSuiteNode> = z.lazy(() =>
  z.object({
    id: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
    parentNodeId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/).nullable(),
    kind: z.string(),
    code: z.string(),
    label: z.string(),
    description: z.string(),
    status: z.string(),
    sortOrder: z.number(),
    actionCodes: z.array(z.string()).default([]),
    metadata: SystemSuiteNodeMetadataSchema.nullable().optional(),
    children: z.array(SystemSuiteNodeSchema).default([]),
  })
);

export const SystemSuiteModuleSchema = z.object({
  id: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  code: z.string(),
  name: z.string(),
  description: z.string(),
  status: z.string(),
  sortOrder: z.number(),
  nodes: z.array(SystemSuiteNodeSchema).optional().default([]),
});

export const SystemSuiteCrudOperationSchema = z.object({
  id: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  code: z.string(),
  name: z.string(),
  description: z.string(),
  sortOrder: z.number(),
});

export const SystemSuiteCustomActionSchema = z.object({
  id: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  code: z.string(),
  name: z.string(),
  description: z.string(),
  sortOrder: z.number(),
});

export const SystemSuiteDomainResourceSchema = z.object({
  id: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  moduleId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/).nullable().optional(),
  parentResourceId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/).nullable().optional(),
  type: z.enum(['Aggregate', 'Entity', 'DomainMethod']),
  code: z.string(),
  name: z.string(),
  description: z.string(),
  crudOperations: z.array(SystemSuiteCrudOperationSchema).optional().default([]),
  customActions: z.array(SystemSuiteCustomActionSchema).optional().default([]),
});

export const SystemSuiteSchema = z.object({
  systemSuiteId: GuidSchema,
  tenantId: GuidSchema,
  code: z.string().min(1),
  name: z.string().min(1),
  description: z.string(),
  status: SystemStatusSchema,
  modules: z.array(SystemSuiteModuleSchema).optional().default([]),
  actions: z.array(SystemSuiteActionSchema).optional().default([]),
  domainResources: z.array(SystemSuiteDomainResourceSchema).optional().default([]),
});

export const SystemSuiteListSchema = z.array(SystemSuiteSchema);

export const SystemSuitePageSchema = z.object({
  items: SystemSuiteListSchema,
  page: z.number().int().min(1),
  pageSize: z.number().int().min(1),
  totalItems: z.number().int().min(0),
  totalPages: z.number().int().min(0),
});

export const CreateSystemSuitePayloadSchema = z.object({
  // `GuidSchema` (regex laxo), NO `z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/)`: Zod v4 exige los bits de
  // variante RFC 4122 en `.uuid()`, demasiado estricto para los GUID de SQL Server
  // (p. ej. el tenant sembrado `5f4e3d2c-…`). Con `.uuid()` el `validate()` del
  // formulario devolvía null y bloqueaba silenciosamente la creación (sin request
  // ni toast). Se alinea con `permission-template.schema.ts` y `profile.schema.ts`,
  // que ya usan el mismo regex laxo por esta razón.
  tenantId: GuidSchema,
  code: z.string().min(1).max(50),
  name: z.string().min(1).max(120),
  // Requerida: el backend `CreateSystemSuiteCommandValidator` exige
  // `Description.NotEmpty().MaximumLength(500)`. Un POST sin descripción devolvía
  // 400. El frontend se alinea al contrato (fuente de verdad = backend) para que
  // el usuario reciba la validación antes de la llamada. Ver G-107.
  description: z.string().min(1, 'Descripción requerida').max(500, 'Máximo 500 caracteres'),
});

export const CreateSystemSuiteResponseSchema = z.object({
  systemSuiteId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
});

export type SystemSuite = z.infer<typeof SystemSuiteSchema>;
export type SystemSuitePage = z.infer<typeof SystemSuitePageSchema>;
export type SystemStatus = z.infer<typeof SystemStatusSchema>;
export type CreateSystemSuitePayload = z.infer<typeof CreateSystemSuitePayloadSchema>;
export type CreateSystemSuiteResponse = z.infer<typeof CreateSystemSuiteResponseSchema>;
export type SystemSuiteDomainResource = z.infer<typeof SystemSuiteDomainResourceSchema>;
export type SystemSuiteCrudOperation = z.infer<typeof SystemSuiteCrudOperationSchema>;
export type SystemSuiteCustomAction = z.infer<typeof SystemSuiteCustomActionSchema>;
