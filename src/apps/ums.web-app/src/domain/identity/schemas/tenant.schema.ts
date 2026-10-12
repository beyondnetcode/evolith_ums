/**
 * tenant.schema.ts — Runtime validation schemas (Zod)
 *
 * These schemas validate API responses at the infrastructure boundary so
 * TypeScript types are guaranteed to match reality at runtime, not just
 * at compile time.
 */
import { z } from 'zod';

export const TenantStatusSchema = z.enum(['Active', 'Suspended', 'Pending']);

// Relaxed UUID: any 8-4-4-4-12 hex string (seed data uses non-RFC-4122 GUIDs)
const GuidSchema = z
  .string()
  .regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/);

export const TenantSchema = z.object({
  tenantId: GuidSchema,
  code: z.string().min(1),
  name: z.string().min(1),
  type: z.string().min(1),
  status: TenantStatusSchema,
  parentTenantId: GuidSchema.nullable(),
  companyReference: z
    .string()
    .nullable()
    .optional()
    .transform(v => v ?? null),
  isManagementOwner: z.boolean().default(false),
});

export const TenantListSchema = z.array(TenantSchema);

export const TenantPageSchema = z.object({
  items: TenantListSchema,
  page: z.number().int().min(1),
  pageSize: z.number().int().min(1),
  totalItems: z.number().int().min(0),
  totalPages: z.number().int().min(0),
});

export const CreateTenantPayloadSchema = z.object({
  code: z.string().min(1).max(20),
  name: z.string().min(1).max(120),
  type: z.string().min(1),
  companyReference: z.string().optional(),
});

// La API `POST /tenants` devuelve solo `{ tenantId }` (201 Created). El nombre y el
// código los conoce el cliente desde el payload que envió; no se esperan en la
// respuesta (exigirlos rompía el parse pese al 201 → G-078).
export const CreateTenantResponseSchema = z.object({
  tenantId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
});

// Infer TS types from schemas (single source of truth)
export type Tenant = z.infer<typeof TenantSchema>;
export type TenantPage = z.infer<typeof TenantPageSchema>;
export type TenantStatus = z.infer<typeof TenantStatusSchema>;
export type CreateTenantPayload = z.infer<typeof CreateTenantPayloadSchema>;
export type CreateTenantResponse = z.infer<typeof CreateTenantResponseSchema>;
