import { z } from 'zod';

const guidSchema = z
  .string()
  .regex(
    /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/,
    'Invalid GUID format'
  );

export const TemplateItemOriginalSchema = z.object({
  itemId: guidSchema,
  targetType: z.string(),
  targetId: guidSchema,
  targetName: z.string(),
  actionId: guidSchema,
  actionName: z.string(),
  isAllowed: z.boolean(),
  isDenied: z.boolean(),
  isActive: z.boolean(),
});

export const ProfilePermissionSchema = z.object({
  permissionId: guidSchema,
  profileId: guidSchema,
  templateId: guidSchema,
  targetType: z.string(),
  targetId: guidSchema,
  targetName: z.string(),
  actionId: guidSchema,
  actionName: z.string(),
  isAllowed: z.boolean(),
  isDenied: z.boolean(),
  isActive: z.boolean(),
  isOverride: z.boolean(),
  originalFromTemplate: TemplateItemOriginalSchema.nullable().optional(),
});

export const ProfileSchema = z.object({
  profileId: guidSchema,
  tenantId: guidSchema,
  tenantCode: z.string(),
  tenantName: z.string(),
  userId: guidSchema,
  userEmail: z.string(),
  roleId: guidSchema,
  roleCode: z.string(),
  roleName: z.string(),
  systemSuiteId: guidSchema,
  systemSuiteCode: z.string(),
  systemSuiteName: z.string(),
  branchId: guidSchema.nullable().optional(),
  branchName: z.string().nullable().optional(),
  scope: z.string(),
  isActive: z.boolean(),
  permissionCount: z.number().int().min(0).default(0),
  permissions: z.array(ProfilePermissionSchema).optional().default([]),
});

export const ProfilePageSchema = z.object({
  items: z.array(ProfileSchema),
  page: z.number().int().min(1),
  pageSize: z.number().int().min(1),
  totalItems: z.number().int().min(0),
  totalPages: z.number().int().min(0),
});

export const CreateProfileResponseSchema = z.object({
  profileId: guidSchema,
});

export type ProfilePermission = z.infer<typeof ProfilePermissionSchema>;
export type Profile = z.infer<typeof ProfileSchema>;
export type ProfilePage = z.infer<typeof ProfilePageSchema>;
export type CreateProfileResponse = z.infer<typeof CreateProfileResponseSchema>;

export interface CreateProfilePayload {
  tenantId: string;
  userId: string;
  roleId: string;
  branchId?: string | null;
}
