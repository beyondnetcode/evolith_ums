/**
 * tenant-signup-request.schema.ts — Runtime validation schemas (Zod)
 *
 * Validates tenant signup request payloads at the infrastructure boundary.
 */
import { z } from 'zod';

const GuidSchema = z
  .string()
  .regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/);

export const TenantSignupRequestSchema = z.object({
  tenantSignupRequestId: GuidSchema,
  companyName: z.string().min(1),
  companyReference: z.string().min(1),
  contactName: z.string().min(1),
  contactEmail: z.string().email(),
  status: z.string().min(1),
  approvedTenantId: GuidSchema.nullable()
    .optional()
    .transform(value => value ?? null),
  requestedAtUtc: z.string().datetime(),
});

export const TenantSignupRequestListSchema = z.array(TenantSignupRequestSchema);

export const ApproveTenantSignupResponseSchema = z.object({
  tenantId: GuidSchema,
  userAccountId: GuidSchema,
  temporaryPassword: z.string().min(1),
  message: z.string().min(1),
});

export type TenantSignupRequest = z.infer<typeof TenantSignupRequestSchema>;
export type TenantSignupRequestList = z.infer<typeof TenantSignupRequestListSchema>;
export type ApproveTenantSignupResponse = z.infer<typeof ApproveTenantSignupResponseSchema>;
