import React, { useMemo, useState } from 'react';
import {
  Shield,
  CheckCircle2,
  XCircle,
  Clock3,
  User,
  Layers,
  GitBranch,
  ChevronsUp,
  ChevronsDown,
  ChevronDown,
  ChevronRight,
} from 'lucide-react';
import { StatusBadge } from '@shared/components/StatusBadge';
import { ListToolbar } from '@shared/components/ListToolbar';
import { DataList } from '@shared/components/data-display/DataList';
import {
  useGetProfileRequests,
  useApproveProfileRequest,
  useRejectProfileRequest,
} from '@app/identity/hooks/use-inbox';
import { useDateFormat } from '@app/formatting/use-date-format';
import type { PendingProfileRequest } from '@domain/identity/schemas/inbox.schema';

const PAGE_SIZE = 6 as const;
const shortId = (id: string) => id.slice(0, 8).toUpperCase();

// ─── Item ─────────────────────────────────────────────────────────────────────

const ProfileRequestItem: React.FC<{
  item: PendingProfileRequest;
  expanded: boolean;
  onToggle: () => void;
  onApprove: (id: string, roleId: string) => void;
  onReject: (id: string) => void;
  isPending: boolean;
  viewMode: 'list' | 'thumbnail';
}> = ({ item, expanded, onToggle, onApprove, onReject, isPending, viewMode }) => {
  const { formatDateTime } = useDateFormat();

  const summary = (
    <div className="flex items-center gap-3 min-w-0">
      <div className="p-1.5 bg-violet-500/10 rounded-lg shrink-0">
        <Shield className="w-3.5 h-3.5 text-violet-600" />
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-xs font-semibold text-m3-on-surface truncate">Solicitud de perfil</p>
        <p className="text-[11px] text-m3-secondary font-mono truncate">
          Usuario {shortId(item.targetUserId)}
        </p>
      </div>
      <StatusBadge status="Pending" label="Pendiente" />
    </div>
  );

  const preview = (
    <div className="space-y-3 pt-2">
      <div className="grid grid-cols-1 gap-1.5 text-[11px] text-m3-secondary">
        <div className="flex items-center gap-1.5">
          <User className="w-3 h-3 shrink-0 text-violet-500" />
          <span className="font-mono text-[10px]">Usuario: {item.targetUserId}</span>
        </div>
        <div className="flex items-center gap-1.5">
          <Layers className="w-3 h-3 shrink-0 text-violet-500" />
          <span className="font-mono text-[10px]">Sistema: {item.requestedSystemId}</span>
        </div>
        <div className="flex items-center gap-1.5">
          <Shield className="w-3 h-3 shrink-0 text-violet-500" />
          <span className="font-mono text-[10px]">Rol: {item.requestedRoleId}</span>
        </div>
        {item.requestedBranchId && (
          <div className="flex items-center gap-1.5">
            <GitBranch className="w-3 h-3 shrink-0 text-violet-500" />
            <span className="font-mono text-[10px]">Sucursal: {item.requestedBranchId}</span>
          </div>
        )}
        {item.justification && (
          <div className="bg-m3-surface-container/40 rounded-lg p-2 text-[11px] italic leading-snug">
            "{item.justification}"
          </div>
        )}
        <div className="flex items-center gap-1.5">
          <Clock3 className="w-3 h-3 shrink-0" />
          <span>{formatDateTime(item.requestedAt) ?? item.requestedAt}</span>
        </div>
      </div>
      <div className="flex items-center gap-2 pt-1">
        <button
          type="button"
          disabled={isPending}
          onClick={() => onApprove(item.approvalRequestId, item.requestedRoleId)}
          className="h-7 px-3 rounded-full bg-violet-600 text-white text-[11px] font-medium hover:bg-violet-500 disabled:opacity-50 transition-colors flex items-center gap-1.5"
        >
          <CheckCircle2 className="w-3 h-3" /> Aprobar
        </button>
        <button
          type="button"
          disabled={isPending}
          onClick={() => onReject(item.approvalRequestId)}
          className="h-7 px-3 rounded-full border border-rose-500/30 text-rose-500 text-[11px] font-medium hover:bg-rose-500/10 disabled:opacity-50 transition-colors flex items-center gap-1.5"
        >
          <XCircle className="w-3 h-3" /> Rechazar
        </button>
      </div>
    </div>
  );

  if (viewMode === 'list') {
    return (
      <div className="border-b border-m3-outline/10 last:border-0">
        <div className="flex items-center gap-2 px-3 py-2.5 hover:bg-m3-surface-container/30 transition-colors">
          <button
            type="button"
            onClick={onToggle}
            className="shrink-0 p-0.5 rounded text-m3-secondary/50 hover:text-m3-primary transition-colors"
          >
            {expanded ? (
              <ChevronDown className="w-3.5 h-3.5" />
            ) : (
              <ChevronRight className="w-3.5 h-3.5" />
            )}
          </button>
          <div className="flex-1 min-w-0">{summary}</div>
        </div>
        {expanded && (
          <div className="px-8 pb-3 pt-1 bg-m3-surface-container/20 border-t border-m3-outline/10 animate-fadeIn">
            {preview}
          </div>
        )}
      </div>
    );
  }

  return (
    <div
      className={`rounded-xl border overflow-hidden transition-all duration-200 ${expanded ? 'border-violet-500/30 bg-m3-surface' : 'border-m3-outline/20 bg-m3-surface/70'}`}
    >
      <button
        type="button"
        onClick={onToggle}
        className="w-full text-left p-4 hover:bg-m3-surface-container/20 transition-colors"
      >
        <div className="flex items-start justify-between gap-2">
          <div className="flex-1 min-w-0">{summary}</div>
          {expanded ? (
            <ChevronDown className="w-4 h-4 text-violet-600 shrink-0 mt-0.5" />
          ) : (
            <ChevronRight className="w-4 h-4 text-m3-secondary/50 shrink-0 mt-0.5" />
          )}
        </div>
      </button>
      {expanded && (
        <div className="px-4 pb-4 pt-1 border-t border-m3-outline/10 animate-fadeIn">{preview}</div>
      )}
    </div>
  );
};

// ─── Panel ────────────────────────────────────────────────────────────────────

export const ProfileRequestsInboxPanel: React.FC = () => {
  const { data, isLoading } = useGetProfileRequests(true);
  const approveMutation = useApproveProfileRequest();
  const rejectMutation = useRejectProfileRequest();
  const isPending = approveMutation.isPending || rejectMutation.isPending;

  const [isCollapsed, setIsCollapsed] = useState(false);
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('thumbnail');
  const [searchValue, setSearchValue] = useState('');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [page, setPage] = useState(1);
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const filtered = useMemo(() => {
    const q = searchValue.trim().toLowerCase();
    let list = data ?? [];
    if (q)
      list = list.filter(
        r =>
          r.targetUserId.toLowerCase().includes(q) ||
          (r.justification ?? '').toLowerCase().includes(q)
      );
    const factor = sortOrder === 'asc' ? 1 : -1;
    return [...list].sort(
      (a, b) => factor * (new Date(a.requestedAt).getTime() - new Date(b.requestedAt).getTime())
    );
  }, [data, searchValue, sortOrder]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const paginated = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const count = data?.length ?? 0;

  return (
    <div className="border border-m3-outline/25 bg-m3-surface-container/20 rounded-2xl overflow-hidden shadow-sm">
      {/* Header */}
      <div className="flex items-center justify-between gap-3 px-4 py-3 border-b border-m3-outline/15 bg-m3-surface/50">
        <div className="flex items-center gap-2.5 min-w-0">
          <Shield className="w-4 h-4 text-violet-600 shrink-0" />
          <div className="min-w-0">
            <h3 className="text-[12px] font-semibold uppercase tracking-wider text-m3-on-surface">
              Solicitudes de Perfil
            </h3>
            <p className="text-[11px] text-m3-secondary truncate">
              Usuarios que solicitan acceso a un perfil o rol.
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <StatusBadge
            status={count > 0 ? 'Pending' : 'Active'}
            label={count > 0 ? `${count} pendientes` : 'Sin pendientes'}
          />
          <button
            type="button"
            title={isCollapsed ? 'Expandir' : 'Colapsar'}
            onClick={() => setIsCollapsed(v => !v)}
            className={`p-1.5 rounded-lg border transition-all duration-150 ${isCollapsed ? 'bg-m3-primary/10 border-m3-primary/40 text-m3-primary' : 'bg-m3-surface-container/60 border-m3-outline/40 text-m3-secondary hover:bg-m3-primary/10 hover:border-m3-primary/40 hover:text-m3-primary'}`}
          >
            {isCollapsed ? (
              <ChevronsDown className="w-3.5 h-3.5" />
            ) : (
              <ChevronsUp className="w-3.5 h-3.5" />
            )}
          </button>
        </div>
      </div>

      {/* Body */}
      <div
        style={{ maxHeight: isCollapsed ? 0 : '580px' }}
        className="overflow-hidden transition-[max-height] duration-300 ease-in-out"
      >
        <ListToolbar
          viewMode={viewMode}
          onViewModeChange={v => {
            setViewMode(v);
            setExpandedId(null);
          }}
          searchOptions={[{ label: 'Usuario / Justificación', value: 'q' }]}
          activeSearchCriteria="q"
          onSearchCriteriaChange={() => {}}
          searchValue={searchValue}
          onSearchValueChange={v => {
            setSearchValue(v);
            setPage(1);
            setExpandedId(null);
          }}
          onSearchSubmit={() => {}}
          onSearchClear={() => {
            setSearchValue('');
            setPage(1);
          }}
          sortOptions={[{ label: 'Fecha', value: 'date' }]}
          sortBy="date"
          onSortByChange={() => {}}
          sortOrder={sortOrder}
          onSortOrderToggle={() => setSortOrder(o => (o === 'asc' ? 'desc' : 'asc'))}
          itemCount={filtered.length}
          itemLabel="solicitud"
        />
        <div className="p-4">
          <DataList
            isLoading={isLoading}
            isEmpty={filtered.length === 0}
            emptyLabel="No hay solicitudes de perfil pendientes."
            emptyTitle="Sin solicitudes"
            viewMode={viewMode}
            renderList={() => (
              <div className="rounded-xl border border-m3-outline/15 overflow-hidden bg-m3-surface/50">
                {paginated.map(item => (
                  <ProfileRequestItem
                    key={item.approvalRequestId}
                    item={item}
                    expanded={expandedId === item.approvalRequestId}
                    onToggle={() =>
                      setExpandedId(v =>
                        v === item.approvalRequestId ? null : item.approvalRequestId
                      )
                    }
                    onApprove={(id, roleId) => void approveMutation.mutate({ id, roleId })}
                    onReject={id => void rejectMutation.mutate({ id })}
                    isPending={isPending}
                    viewMode="list"
                  />
                ))}
              </div>
            )}
            renderThumbnail={() => (
              <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-3">
                {paginated.map(item => (
                  <ProfileRequestItem
                    key={item.approvalRequestId}
                    item={item}
                    expanded={expandedId === item.approvalRequestId}
                    onToggle={() =>
                      setExpandedId(v =>
                        v === item.approvalRequestId ? null : item.approvalRequestId
                      )
                    }
                    onApprove={(id, roleId) => void approveMutation.mutate({ id, roleId })}
                    onReject={id => void rejectMutation.mutate({ id })}
                    isPending={isPending}
                    viewMode="thumbnail"
                  />
                ))}
              </div>
            )}
            pagination={
              totalPages > 1
                ? {
                    page,
                    pageSize: PAGE_SIZE,
                    totalItems: filtered.length,
                    totalPages,
                    onPageChange: setPage,
                  }
                : undefined
            }
          />
        </div>
      </div>
    </div>
  );
};
