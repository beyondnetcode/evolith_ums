import React from 'react';
import { Clock3, DatabaseZap, FileClock, UserRound } from 'lucide-react';
import type { AppConfiguration } from '@domain/configuration/schemas/app-configuration.schema';
import { useGetAuditRecords } from '@app/audit/hooks/use-audit-records';
import { useI18n } from '@app/i18n/use-i18n';
import { DetailSection } from '@shared/components/DetailSection';
import { CodeBadge } from '@shared/components/CodeBadge';
import { StatusBadge } from '@shared/components/StatusBadge';

interface AppConfigurationAuditTrailPanelProps {
  config: AppConfiguration;
}

const DATE_FORMAT = new Intl.DateTimeFormat('es-PE', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

export function AppConfigurationAuditTrailPanel({
  config,
}: AppConfigurationAuditTrailPanelProps): React.JSX.Element {
  const t = useI18n();
  const { data, isLoading, error } = useGetAuditRecords(
    {
      page: 1,
      pageSize: 5,
      entityId: config.appConfigurationId,
      entityType: 'AppConfiguration',
      tenantId: config.tenantId ?? undefined,
    },
    !!config.appConfigurationId
  );

  const records = data?.items ?? [];

  return (
    <DetailSection
      title={t.auditTrail ?? 'Audit Trail'}
      subtitle={t.auditTrailSubtitle ?? 'Latest configuration changes'}
      content={
        <div className="space-y-3">
          {error && (
            <p className="text-[11px] text-rose-600">
              {t.auditTrailLoadFailed ?? 'Could not load audit trail.'}
            </p>
          )}
          {isLoading && (
            <p className="text-[11px] text-m3-secondary">{t.loading ?? 'Loading...'}</p>
          )}
          {!isLoading && records.length === 0 && !error && (
            <p className="text-[11px] text-m3-secondary">
              {t.auditTrailEmpty ?? 'No audit entries found for this configuration.'}
            </p>
          )}
          <div className="space-y-2">
            {records.map(record => (
              <div
                key={record.auditRecordId}
                className="rounded-xl border border-m3-outline/15 bg-m3-surface-container/20 p-3 space-y-2"
              >
                <div className="flex flex-wrap items-center gap-2">
                  <CodeBadge code={record.eventType} size="xs" />
                  <StatusBadge status={record.auditResult} label={record.auditResult} size="xs" />
                  <span className="inline-flex items-center gap-1 text-[10px] text-m3-secondary">
                    <Clock3 className="w-3 h-3" />
                    {DATE_FORMAT.format(new Date(record.whenOccurred))}
                  </span>
                </div>
                <p className="text-[11px] text-m3-on-surface leading-relaxed">
                  {record.whatChanged}
                </p>
                <div className="flex flex-wrap gap-2 text-[10px] text-m3-secondary">
                  <span className="inline-flex items-center gap-1">
                    <UserRound className="w-3 h-3" />
                    {record.whoActed.substring(0, 8)}...
                  </span>
                  <span className="inline-flex items-center gap-1">
                    <DatabaseZap className="w-3 h-3" />
                    {record.affectedEntityType}
                  </span>
                  <span className="inline-flex items-center gap-1">
                    <FileClock className="w-3 h-3" />
                    Tenant {record.rootTenantId.substring(0, 8)}...
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>
      }
    />
  );
}
