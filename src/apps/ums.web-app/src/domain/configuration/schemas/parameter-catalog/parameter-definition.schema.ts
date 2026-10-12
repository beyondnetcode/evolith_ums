import { z } from 'zod';

export const ParameterDefinitionSchema = z.object({
  id: z
    .string()
    .regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/, {
      message: 'Invalid UUID format',
    }),
  code: z.string().min(1).max(100),
  name: z.string().min(1).max(200),
  description: z.string().max(1000).nullable(),
  dataTypeId: z.number().int().min(1).max(4),
  dataTypeName: z.string(),
  defaultValue: z.string(),
  scopeId: z.number().int().min(1).max(3),
  scopeName: z.enum(['GlobalOnly', 'TenantOnly', 'GlobalAndTenant']),
  isActive: z.boolean(),
  isMandatory: z.boolean(),
  displayOrder: z.number().int().nonnegative(),
  version: z.string().regex(/^\d+\.\d+\.\d+$/),
});

export type ParameterDefinition = z.infer<typeof ParameterDefinitionSchema>;

export const ParameterDefinitionPageSchema = z.object({
  items: z.array(ParameterDefinitionSchema),
  totalItems: z.number().int().nonnegative(),
});

export type ParameterDefinitionPage = z.infer<typeof ParameterDefinitionPageSchema>;

export const ParameterDefinitionFilterSchema = z.object({
  search: z.string().optional(),
  scopeId: z.number().int().optional(),
  isActive: z.boolean().optional(),
});

export type ParameterDefinitionFilter = z.infer<typeof ParameterDefinitionFilterSchema>;

export const DataTypeLabels: Record<number, string> = {
  1: 'String',
  2: 'Number',
  3: 'Boolean',
  4: 'Json',
};

export const ScopeLabels: Record<number, string> = {
  1: 'Global Only',
  2: 'Tenant Only',
  3: 'Global & Tenant',
};

export const CreateParameterDefinitionSchema = z.object({
  code: z.string().min(1).max(100),
  name: z.string().min(1).max(200),
  description: z.string().max(1000).nullable(),
  dataTypeId: z.number().int().min(1).max(4),
  defaultValue: z.string(),
  scopeId: z.number().int().min(1).max(3),
  isActive: z.boolean(),
  isMandatory: z.boolean(),
  displayOrder: z.number().int().nonnegative(),
});

export type CreateParameterDefinitionPayload = z.infer<typeof CreateParameterDefinitionSchema>;

export const UpdateParameterDefinitionSchema = CreateParameterDefinitionSchema.partial();

export type UpdateParameterDefinitionPayload = z.infer<typeof UpdateParameterDefinitionSchema>;
