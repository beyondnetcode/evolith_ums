/**
 * permission-template.schema.ts — Zod runtime validation schemas.
 *
 * PermissionTemplate lifecycle:  Draft → Published → Deprecated
 * PermissionTemplateItem effect: isAllowed=true ✓ | isDenied=true ✗ | both false ~
 * ExclusiveArcTarget:            SystemSuite | Module | Submodule | Option | Aggregate | Entity
 *   Los dos últimos apuntan a un objeto de dominio del catálogo de la suite; el backend los
 *   admite desde G-192 (antes su validador copiaba a mano solo los cuatro de navegación).
 */
import { z } from 'zod';

// Zod v4 enforces RFC 4122 variant bits in .uuid() — too strict for SQL Server
// GUIDs which may not satisfy those bit constraints.  Use a format-only check.
const guidSchema = z
  .string()
  .regex(
    /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/,
    'Invalid GUID format'
  );

export const TemplateStatusSchema = z.enum(['Draft', 'Published', 'Deprecated']);
export const ExclusiveArcTargetSchema = z.enum([
  'SystemSuite',
  'Module',
  'Submodule',
  'Option',
  'Aggregate',
  'Entity',
]);
export const PermissionEffectSchema = z.enum(['Allow', 'Deny', 'Neutral']);

export const PermissionTemplateItemSchema = z.object({
  itemId: guidSchema,
  targetType: ExclusiveArcTargetSchema,
  targetId: guidSchema,
  targetName: z.string(),
  actionId: guidSchema,
  actionName: z.string(),
  isAllowed: z.boolean(),
  isDenied: z.boolean(),
  isActive: z.boolean(),
});

export const PermissionTemplateSchema = z.object({
  templateId: guidSchema,
  tenantId: guidSchema,
  roleId: guidSchema,
  roleName: z.string(),
  systemSuiteId: guidSchema,
  systemSuiteName: z.string(),
  version: z.string(),
  status: TemplateStatusSchema,
});

export const PermissionTemplateDetailSchema = PermissionTemplateSchema.extend({
  items: z.array(PermissionTemplateItemSchema),
});

export const PermissionTemplatePageSchema = z.object({
  items: z.array(PermissionTemplateSchema),
  page: z.number().int().min(1),
  pageSize: z.number().int().min(1),
  totalItems: z.number().int().min(0),
  totalPages: z.number().int().min(0),
});

export const CreatePermissionTemplateResponseSchema = z.object({
  templateId: guidSchema,
});

export type TemplateStatus = z.infer<typeof TemplateStatusSchema>;
export type ExclusiveArcTarget = z.infer<typeof ExclusiveArcTargetSchema>;
export type PermissionEffect = z.infer<typeof PermissionEffectSchema>;
export type PermissionTemplateItem = z.infer<typeof PermissionTemplateItemSchema>;
export type PermissionTemplate = z.infer<typeof PermissionTemplateSchema>;
export type PermissionTemplateDetail = z.infer<typeof PermissionTemplateDetailSchema>;
export type PermissionTemplatePage = z.infer<typeof PermissionTemplatePageSchema>;
export type CreatePermissionTemplateResponse = z.infer<
  typeof CreatePermissionTemplateResponseSchema
>;

export interface CreatePermissionTemplatePayload {
  tenantId: string;
  roleId: string;
  systemSuiteId: string;
}

export interface AddTemplateItemPayload {
  targetType: ExclusiveArcTarget;
  targetId: string;
  actionId: string;
  isAllowed: boolean;
  isDenied: boolean;
}

/**
 * Efecto GUARDADO en la fila, ignorando si sigue vigente.
 *
 * Solo interesa en el camino de escritura: al reactivar un ítem retirado hay que saber qué efecto
 * conserva para no lanzar un PUT redundante. Para pintar la pantalla se usa `itemEffect`.
 */
export function itemStoredEffect(
  item: Pick<PermissionTemplateItem, 'isAllowed' | 'isDenied'>
): PermissionEffect {
  if (item.isAllowed) return 'Allow';
  if (item.isDenied) return 'Deny';
  return 'Neutral';
}

/**
 * Un ítem retirado sobrevive en la tabla con `isActive: false` (ADR-0164: solo hay borrado lógico).
 *
 * La ausencia del campo se interpreta como vigente: hay llamadores que derivan el efecto de un
 * objeto parcial (`{ isAllowed, isDenied }`) y ahí no hay retiro que valga.
 */
export function isRetiredItem(item: Partial<Pick<PermissionTemplateItem, 'isActive'>>): boolean {
  return item.isActive === false;
}

/**
 * Efecto EFECTIVO de la concesión: lo que el ítem realmente concede hoy.
 *
 * Un ítem retirado no concede nada —el backend es fail-closed—, así que se deriva a `Neutral`
 * aunque conserve `isAllowed: true`. Mirar solo los booleanos hacía que la pantalla pintara como
 * `Allow` una fila que ya no autoriza nada.
 */
export function itemEffect(
  item: Pick<PermissionTemplateItem, 'isAllowed' | 'isDenied'> &
    Partial<Pick<PermissionTemplateItem, 'isActive'>>
): PermissionEffect {
  if (isRetiredItem(item)) return 'Neutral';
  return itemStoredEffect(item);
}

/**
 * De varias filas sobre el mismo objetivo, la que manda para editar es la vigente; si todas están
 * retiradas, la primera (que es la que ocupa la clave y habrá que reactivar).
 */
export function primaryItem<T extends Partial<Pick<PermissionTemplateItem, 'isActive'>>>(
  items: readonly T[]
): T | undefined {
  return items.find(item => !isRetiredItem(item)) ?? items[0];
}
