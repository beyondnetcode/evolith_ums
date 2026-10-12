/**
 * audit-record.service.ts
 *
 * REST client for audit history reads.
 */
import { httpClient } from '@infra/http/httpClient';
import { logger } from '@app/utils/logger';
import {
  AuditRecordPageSchema,
  type AuditRecordPage,
} from '@domain/audit/schemas/audit-record.schema';

function buildQueryString(params?: {
  page?: number;
  pageSize?: number;
  eventType?: string;
  actorId?: string;
  entityId?: string;
  entityType?: string;
  tenantId?: string;
  from?: string;
  to?: string;
}): string {
  const searchParams = new URLSearchParams();
  if (!params) return searchParams.toString();

  searchParams.set('page', String(params.page ?? 1));
  searchParams.set('pageSize', String(params.pageSize ?? 20));
  if (params.eventType) searchParams.set('eventType', params.eventType);
  if (params.actorId) searchParams.set('actorId', params.actorId);
  if (params.entityId) searchParams.set('entityId', params.entityId);
  if (params.entityType) searchParams.set('entityType', params.entityType);
  if (params.tenantId) searchParams.set('tenantId', params.tenantId);
  if (params.from) searchParams.set('from', params.from);
  if (params.to) searchParams.set('to', params.to);
  return searchParams.toString();
}

export const auditRecordService = {
  getAll: async (params?: {
    page?: number;
    pageSize?: number;
    eventType?: string;
    actorId?: string;
    entityId?: string;
    entityType?: string;
    tenantId?: string;
    from?: string;
    to?: string;
  }): Promise<AuditRecordPage> => {
    const qs = buildQueryString(params);
    const { data } = await httpClient.get<unknown>(`/audit-records?${qs}`);
    const result = AuditRecordPageSchema.safeParse(data);
    if (!result.success) {
      logger.error('Invalid REST response shape for audit records query', result.error);
      throw new Error('Invalid REST response shape for audit records query');
    }
    return result.data;
  },
};

export default auditRecordService;
