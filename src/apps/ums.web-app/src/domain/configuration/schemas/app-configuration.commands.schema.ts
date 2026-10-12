import { z } from 'zod';

export const CreateAppConfigurationPayloadSchema = z.object({
  tenantId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/).nullable().optional(),
  systemSuiteId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/).nullable().optional(),
  moduleId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/).nullable().optional(),
  code: z.string().min(1).max(100),
  value: z.string(),
  description: z.string().max(500).optional().default(''),
  isInheritable: z.boolean().optional().default(false),
  isEncrypted: z.boolean().optional().default(false),
});

export type CreateAppConfigurationPayload = z.infer<typeof CreateAppConfigurationPayloadSchema>;

export const UpdateAppConfigurationPayloadSchema = z.object({
  value: z.string(),
  description: z.string().max(500).optional(),
});

export type UpdateAppConfigurationPayload = z.infer<typeof UpdateAppConfigurationPayloadSchema>;
