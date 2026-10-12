import { z } from 'zod';

const GuidSchema = z
  .string()
  .regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/);

export const RoleSchema = z.object({
  roleId: GuidSchema,
  tenantId: GuidSchema,
  systemSuiteId: GuidSchema,
  parentRoleId: GuidSchema.nullable(),
  code: z.string().min(1),
  value: z.string().min(1),
  description: z.string(),
  hierarchyLevel: z.number().int().min(0),
  promotionOrder: z.number().int().min(0),
  isActive: z.boolean(),
});

export const RoleListSchema = z.array(RoleSchema);

export const CreateRoleResponseSchema = z.object({
  roleId: GuidSchema,
});

export type Role = z.infer<typeof RoleSchema>;
export type CreateRoleResponse = z.infer<typeof CreateRoleResponseSchema>;

export interface CreateRolePayload {
  code: string;
  value: string;
  description: string;
  parentRoleId: string | null;
  hierarchyLevel: number;
  promotionOrder: number;
}

export interface UpdateRolePayload {
  value: string;
  description: string;
  parentRoleId: string | null;
  hierarchyLevel: number;
  promotionOrder: number;
}
