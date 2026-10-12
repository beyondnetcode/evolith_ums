import { z } from 'zod';

const GuidSchema = z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/);

export const PendingUserSignupSchema = z.object({
  userAccountId: GuidSchema,
  tenantId: GuidSchema,
  email: z.string().email(),
  displayName: z
    .string()
    .nullable()
    .optional()
    .transform(v => v ?? null),
  category: z.string(),
  requestedAt: z.string(),
});

export const PendingProfileRequestSchema = z.object({
  approvalRequestId: GuidSchema,
  targetUserId: GuidSchema,
  requestedSystemId: GuidSchema,
  requestedBranchId: GuidSchema.nullable()
    .optional()
    .transform(v => v ?? null),
  requestedRoleId: GuidSchema,
  justification: z
    .string()
    .nullable()
    .optional()
    .transform(v => v ?? null),
  requestedAt: z.string(),
});

export const PendingUserSignupListSchema = z.array(PendingUserSignupSchema);
export const PendingProfileRequestListSchema = z.array(PendingProfileRequestSchema);

export type PendingUserSignup = z.infer<typeof PendingUserSignupSchema>;
export type PendingProfileRequest = z.infer<typeof PendingProfileRequestSchema>;
