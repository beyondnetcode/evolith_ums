import React, { useState } from 'react';
import { ChevronLeft, ChevronRight, Clock3, DatabaseZap, FileClock, UserRound } from 'lucide-react';
import { useGetAuditRecords } from '@app/audit/hooks/use-audit-records';
import { DetailSection } from '@shared/components/DetailSection';
import { CodeBadge } from '@shared/components/CodeBadge';
import { StatusBadge } from '@shared/components/StatusBadge';

interface TenantAuditPanelProps {
  tenantId: string;
}

const PAGE_SIZE = 20;

const DATE_FORMAT = new Intl.DateTimeFormat('es-PE', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

export function TenantAuditPanel({ tenantId }: TenantAuditPanelProps): React.JSX.Element {
  const [page, setPage] = useState(1);

  const { data, isLoading, error } = useGetAuditRecords(
    {
      page,
      pageSize: PAGE_SIZE,
      tenantId,
    },
    !!tenantId
  );

  const records = data?.items ?? [];
  const totalItems = data?.totalItems ?? 0;
  const totalPages = data?.totalPages ?? 0;

  return (
    <DetailSection
      title="Auditoría"
      subtitle="Historial de cambios registrados para este tenant"
      content={
        <div className="space-y-3">
          {error && (
            <p className="text-[11px] text-rose-600">
              No se pudo cargar el historial de auditoría.
            </p>
          )}
          {isLoading && <p className="text-[11px] text-m3-secondary">Cargando...</p>}
          {!isLoading && records.length === 0 && !error && (
            <p className="text-[11px] text-m3-secondary">
              No se encontraron registros de auditoría para este tenant.
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
                  <StatusBadge status={record.auditResult} label={record.auditResult} />
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

          {!isLoading && !error && totalItems > 0 && (
            <div className="flex items-center justify-between pt-1">
              <span className="text-[10px] text-m3-secondary">
                {totalItems} registro{totalItems === 1 ? '' : 's'} · Página {page} de{' '}
                {Math.max(totalPages, 1)}
              </span>
              <div className="flex items-center gap-1">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage(p => Math.max(1, p - 1))}
                  className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-[10px] text-m3-secondary
                    hover:text-m3-primary hover:bg-m3-primary/10 transition-colors
                    disabled:opacity-40 disabled:pointer-events-none"
                >
                  <ChevronLeft className="w-3 h-3" />
                  Anterior
                </button>
                <button
                  type="button"
                  disabled={page >= totalPages}
                  onClick={() => setPage(p => p + 1)}
                  className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-[10px] text-m3-secondary
                    hover:text-m3-primary hover:bg-m3-primary/10 transition-colors
                    disabled:opacity-40 disabled:pointer-events-none"
                >
                  Siguiente
                  <ChevronRight className="w-3 h-3" />
                </button>
              </div>
            </div>
          )}
        </div>
      }
    />
  );
}
