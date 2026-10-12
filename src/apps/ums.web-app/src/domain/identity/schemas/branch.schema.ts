/**
 * branch.schema.ts — contrato de sucursal validado en frontera (Zod).
 *
 * ADR-0164: la baja de una sucursal dejó de ser un borrado. Ahora hay DOS verbos distintos y la
 * diferencia importa mucho para quien opera:
 *
 *  - Desactivar / reactivar → pausa REVERSIBLE. La sucursal sigue existiendo y puede volver.
 *  - Cerrar                 → TERMINAL. No se reactiva nunca y su código queda ocupado para
 *                             siempre; si el negocio reabre la plaza, se da de alta otra sucursal
 *                             con otro código.
 *
 * El listado excluye por defecto las cerradas; hay que pedirlas con `includeClosed=true`.
 */
import { z } from 'zod';

const guidSchema = z
  .string()
  .regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/);

export const BranchSchema = z.object({
  branchId: guidSchema,
  code: z.string().min(1),
  name: z.string().min(1),
  isActive: z.boolean(),
  geofencingMetadata: z
    .string()
    .nullable()
    .optional()
    .transform(v => v ?? null),
  // Opcionales en el parseo, no porque el API los omita, sino para que un backend anterior al
  // cierre no rompa la pantalla: sin el campo, la sucursal simplemente no está cerrada.
  isClosed: z
    .boolean()
    .optional()
    .transform(v => v ?? false),
  closedAtUtc: z
    .string()
    .nullable()
    .optional()
    .transform(v => v ?? null),
});

export const BranchListSchema = z.array(BranchSchema);

export const AddBranchPayloadSchema = z.object({
  code: z.string().min(1).max(20),
  name: z.string().min(1).max(120),
  geofencingMetadata: z.string().optional(),
});

export const AddBranchResponseSchema = z.object({
  branchId: guidSchema,
  tenantId: guidSchema,
  code: z.string(),
});

/**
 * Un asiento de la bitácora. El episodio viaja como nombre ("Opened", "Deactivated",
 * "Reactivated", "Closed") y se valida como cadena libre a propósito: si el backend añade un
 * episodio nuevo, la bitácora debe seguir mostrándose en vez de reventar el parseo.
 */
export const BranchLifecycleEntrySchema = z.object({
  entryId: guidSchema,
  episode: z.string().min(1),
  occurredAtUtc: z.string().min(1),
  actorId: z.string(),
  nameSnapshot: z.string(),
  geofencingSnapshot: z
    .string()
    .nullable()
    .optional()
    .transform(v => v ?? null),
  reason: z
    .string()
    .nullable()
    .optional()
    .transform(v => v ?? null),
});

export const BranchLifecycleSchema = z.array(BranchLifecycleEntrySchema);

export type Branch = z.infer<typeof BranchSchema>;
export type AddBranchPayload = z.infer<typeof AddBranchPayloadSchema>;
export type AddBranchResponse = z.infer<typeof AddBranchResponseSchema>;
export type BranchLifecycleEntry = z.infer<typeof BranchLifecycleEntrySchema>;

/**
 * Estado de una sucursal para la interfaz. Es una unión cerrada porque son tres situaciones
 * excluyentes con acciones distintas, no dos booleanos independientes (ADR-0109 P-FE-01).
 */
export type BranchState = 'Active' | 'Inactive' | 'Closed';

/**
 * El cierre manda sobre la actividad: una sucursal cerrada no admite NINGUNA transición, así que
 * jamás debe pintarse como activa aunque llegara con `isActive: true`.
 */
export function branchState(branch: Pick<Branch, 'isActive' | 'isClosed'>): BranchState {
  if (branch.isClosed) return 'Closed';
  return branch.isActive ? 'Active' : 'Inactive';
}

/** Solo una sucursal abierta y pausada puede volver: el cierre es irreversible (ADR-0164 §2.4). */
export function canReactivateBranch(branch: Pick<Branch, 'isActive' | 'isClosed'>): boolean {
  return branchState(branch) === 'Inactive';
}

/** Solo se pausa lo que está activo; lo cerrado ya terminó su ciclo de vida. */
export function canDeactivateBranch(branch: Pick<Branch, 'isActive' | 'isClosed'>): boolean {
  return branchState(branch) === 'Active';
}

/** Cerrar dos veces no es idempotente en el backend: responde «ya está cerrada». */
export function canCloseBranch(branch: Pick<Branch, 'isActive' | 'isClosed'>): boolean {
  return branchState(branch) !== 'Closed';
}
