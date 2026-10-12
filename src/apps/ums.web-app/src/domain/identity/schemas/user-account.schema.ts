/**
 * user-account.schema.ts — Runtime validation schemas (Zod)
 *
 * Single source of truth for UserAccount types. Validates API responses
 * at the infrastructure boundary so TypeScript types match reality at runtime.
 *
 * Backend enums:
 *   UserStatus: Pending | Active | Blocked
 *   UserCategory: Internal | External | B2B | Partner | ServiceAccount
 *   IdentityReferenceType: HrId | VendorCode | GovernmentId | PartnerRef
 */
import { z } from 'zod';

export const UserStatusSchema = z.enum(['Pending', 'Active', 'Blocked', 'Deleted', 'Denied']);
export const UserCategorySchema = z.enum([
  'Internal',
  'External',
  'B2B',
  'Partner',
  'ServiceAccount',
]);
export const IdentityReferenceTypeSchema = z.enum([
  'HrId',
  'VendorCode',
  'GovernmentId',
  'PartnerRef',
]);

const GuidSchema = z
  .string()
  .regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/);

export const UserAccountSchema = z.object({
  userAccountId: GuidSchema,
  tenantId: GuidSchema,
  branchId: GuidSchema.nullable()
    .optional()
    .transform(v => v ?? null),
  displayName: z
    .string()
    .min(1)
    .nullable()
    .optional()
    .transform(v => v ?? null),
  email: z.string().email(),
  category: UserCategorySchema,
  status: UserStatusSchema,
  identityReference: z
    .string()
    .nullable()
    .optional()
    .transform(v => v ?? null),
  identityReferenceType: IdentityReferenceTypeSchema.nullable()
    .optional()
    .transform(v => v ?? null),
  hasActivePassword: z.boolean().optional().default(false),
  passwordUpdatedAtUtc: z
    .string()
    .nullable()
    .optional()
    .transform(v => v ?? null),
});

export const UserAccountListSchema = z.array(UserAccountSchema);

export const UserAccountPageSchema = z.object({
  items: UserAccountListSchema,
  page: z.number().int().min(1),
  pageSize: z.number().int().min(1),
  totalItems: z.number().int().min(0),
  totalPages: z.number().int().min(0),
});

export const CreateUserAccountPayloadSchema = z.object({
  tenantId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  branchId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/).nullable().optional(),
  email: z.string().email().max(150),
  category: UserCategorySchema,
  identityReference: z.string().max(150).optional(),
  identityReferenceType: IdentityReferenceTypeSchema.optional(),
});

export const CreateUserAccountResponseSchema = z.object({
  userAccountId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
});

export const SetUserAccountPasswordPayloadSchema = z.object({
  userAccountId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  password: z.string().min(12).max(128),
});

export const SetUserAccountPasswordResponseSchema = z.object({
  credentialId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
});

export type UserAccount = z.infer<typeof UserAccountSchema>;
export type UserAccountPage = z.infer<typeof UserAccountPageSchema>;
export type UserStatus = z.infer<typeof UserStatusSchema>;
export type UserCategory = z.infer<typeof UserCategorySchema>;
export type IdentityReferenceType = z.infer<typeof IdentityReferenceTypeSchema>;
export type CreateUserAccountPayload = z.infer<typeof CreateUserAccountPayloadSchema>;
export type CreateUserAccountResponse = z.infer<typeof CreateUserAccountResponseSchema>;
export type SetUserAccountPasswordPayload = z.infer<typeof SetUserAccountPasswordPayloadSchema>;
export type SetUserAccountPasswordResponse = z.infer<typeof SetUserAccountPasswordResponseSchema>;
