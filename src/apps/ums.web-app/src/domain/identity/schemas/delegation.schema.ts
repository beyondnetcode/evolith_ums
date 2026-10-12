/**
 * delegation.schema.ts — Runtime validation schemas (Zod)
 *
 * Single source of truth for UserManagementDelegation types. Validates API
 * responses at the infrastructure boundary so TypeScript types match reality.
 *
 * Backend enums:
 *   DelegationScopeType: Tenant | Organization | Department | System | Team
 *   DelegationStatus: Draft | PendingApproval | Active | Revoked | Expired | Completed | Rejected | Archived
 *   DelegatedAction: CreateUser | BlockUser | AssignProfile | ResetPassword | RevokeMfa
 */
import { z } from 'zod';

const GuidSchema = z
  .string()
  .regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/);

export const DelegationScopeTypeSchema = z.enum([
  'Tenant',
  'Organization',
  'Department',
  'System',
  'Team',
]);

export const DelegationStatusSchema = z.enum([
  'Draft',
  'PendingApproval',
  'Active',
  'Revoked',
  'Expired',
  'Completed',
  'Rejected',
  'Archived',
]);

export const DelegatedActionSchema = z.enum([
  'CreateUser',
  'BlockUser',
  'AssignProfile',
  'ResetPassword',
  'RevokeMfa',
]);

export const DelegationSchema = z.object({
  delegationId: GuidSchema,
  tenantId: GuidSchema,
  delegatingAdminId: GuidSchema,
  delegatedAdminId: GuidSchema,
  scopeType: DelegationScopeTypeSchema,
  scopeId: GuidSchema.nullable()
    .optional()
    .transform(v => v ?? null),
  allowedActions: z.array(DelegatedActionSchema),
  validFrom: z.string(),
  validUntil: z.string(),
  maxDurationDays: z
    .number()
    .int()
    .positive()
    .nullable()
    .optional()
    .transform(v => v ?? null),
  requiresApproval: z.boolean(),
  approvalRequestId: GuidSchema.nullable()
    .optional()
    .transform(v => v ?? null),
  status: DelegationStatusSchema,
  revokedAt: z
    .string()
    .nullable()
    .optional()
    .transform(v => v ?? null),
  revokedBy: GuidSchema.nullable()
    .optional()
    .transform(v => v ?? null),
  revocationReason: z
    .string()
    .nullable()
    .optional()
    .transform(v => v ?? null),
});

export const DelegationListSchema = z.array(DelegationSchema);

export const CreateDelegationPayloadSchema = z.object({
  tenantId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  delegatingAdminId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  delegatedAdminId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  scopeType: DelegationScopeTypeSchema,
  scopeId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/).nullable().optional(),
  allowedActions: z.array(DelegatedActionSchema).min(1),
  validFrom: z.string(),
  validUntil: z.string(),
  maxDurationDays: z.number().int().positive().nullable().optional(),
  requiresApproval: z.boolean(),
});

export const CreateDelegationResponseSchema = z.object({
  delegationId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
});

export const RevokeDelegationPayloadSchema = z.object({
  reason: z.string().min(1).max(500),
});

export type DelegationScopeType = z.infer<typeof DelegationScopeTypeSchema>;
export type DelegationStatus = z.infer<typeof DelegationStatusSchema>;
export type DelegatedAction = z.infer<typeof DelegatedActionSchema>;
export type Delegation = z.infer<typeof DelegationSchema>;
export type CreateDelegationPayload = z.infer<typeof CreateDelegationPayloadSchema>;
export type CreateDelegationResponse = z.infer<typeof CreateDelegationResponseSchema>;
export type RevokeDelegationPayload = z.infer<typeof RevokeDelegationPayloadSchema>;
