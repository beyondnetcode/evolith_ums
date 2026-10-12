import { z } from 'zod';

export const AuditRecordSchema = z.object({
  auditRecordId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  whoActed: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  subjectType: z.string().min(1),
  whenOccurred: z.string().min(1),
  whatChanged: z.string().min(1),
  eventType: z.string().min(1),
  auditResult: z.string().min(1),
  affectedEntityId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  affectedEntityType: z.string().min(1),
  rootTenantId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  metadata: z.string().nullable().optional(),
});

export type AuditRecord = z.infer<typeof AuditRecordSchema>;

export const AuditRecordPageSchema = z.object({
  items: z.array(AuditRecordSchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  totalItems: z.number().int().nonnegative(),
  totalPages: z.number().int().nonnegative(),
});

export type AuditRecordPage = z.infer<typeof AuditRecordPageSchema>;
