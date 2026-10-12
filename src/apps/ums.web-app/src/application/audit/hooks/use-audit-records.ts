import { useQuery } from '@tanstack/react-query';
import { CONTEXT_QUERY_CONFIG } from '@app/shared/config/query.config';
import { auditRecordService } from '@infra/audit/services/audit-record.service';
import type { AuditRecordPage } from '@domain/audit/schemas/audit-record.schema';

export function useGetAuditRecords(
  params?: {
    page?: number;
    pageSize?: number;
    eventType?: string;
    actorId?: string;
    entityId?: string;
    entityType?: string;
    tenantId?: string;
    from?: string;
    to?: string;
  },
  enabled = true
) {
  return useQuery<AuditRecordPage, Error>({
    queryKey: ['audit-records', params],
    queryFn: () => auditRecordService.getAll(params),
    enabled,
    ...CONTEXT_QUERY_CONFIG.APP_CONFIGURATION,
  });
}
