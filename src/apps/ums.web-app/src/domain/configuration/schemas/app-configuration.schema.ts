import { z } from 'zod';

export const AppConfigurationSchema = z.object({
  appConfigurationId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  tenantId: z.string().nullable(),
  systemSuiteId: z.string().nullable(),
  moduleId: z.string().nullable(),
  code: z.string().min(1).max(100),
  value: z.string(),
  description: z.string().max(500),
  scope: z.enum(['Global', 'Tenant', 'Suite', 'Module']),
  isInheritable: z.boolean(),
  isEncrypted: z.boolean(),
  version: z.string().regex(/^\d+\.\d+\.\d+$/),
  status: z.enum(['Draft', 'Published', 'Archived']),
  rowVersion: z.string().nullable().optional(),
});

export type AppConfiguration = z.infer<typeof AppConfigurationSchema>;

export const AppConfigurationPageSchema = z.object({
  items: z.array(AppConfigurationSchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  totalItems: z.number().int().nonnegative(),
  totalPages: z.number().int().nonnegative(),
});

export type AppConfigurationPage = z.infer<typeof AppConfigurationPageSchema>;

export const CreateAppConfigurationResponseSchema = z.object({
  appConfigurationId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  // El backend (`CreateAppConfigurationResponse`) responde 201 con SOLO el id; exigir `code`
  // hacía fallar el parseo del alta y disparaba un toast de error pese al 201. (G-147)
  code: z.string().optional(),
});

export type CreateAppConfigurationResponse = z.infer<typeof CreateAppConfigurationResponseSchema>;
