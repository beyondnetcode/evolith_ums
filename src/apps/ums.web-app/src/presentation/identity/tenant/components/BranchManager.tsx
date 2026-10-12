import React, { useCallback, useState } from 'react';
import {
  useGetBranches,
  useAddBranch,
  useUpdateBranch,
  useCloseBranch,
  useDeactivateBranch,
  useReactivateBranch,
} from '@app/identity/hooks/use-branch';
import { useI18n } from '@app/i18n/use-i18n';
import { useInlineEdit } from '@app/hooks/use-inline-edit';
import { formatSystemCode } from '@app/utils/security';
import { branchState } from '@domain/identity/models/branch.model';
import type { Branch } from '@domain/identity/models/branch.model';
import { M3TextField } from '@shared/components/M3TextField';
import { StatusBadge } from '@shared/components/StatusBadge';
import { EntityRow } from '@shared/components/EntityRow';
import { InlineAddForm } from '@shared/components/InlineAddForm';
import { EmptyState } from '@shared/components/EmptyState';
import { SectionHeader } from '@shared/components/SectionHeader';
import { CodeBadge } from '@shared/components/CodeBadge';
import { IconButton } from '@shared/components/Tooltip';
import { M3Dialog } from '@shared/components/M3Dialog';
import {
  RefreshCw,
  MapPin,
  EyeOff,
  ShieldCheck,
  Pencil,
  Save,
  X,
  Lock,
  History,
} from 'lucide-react';
import { ListToolbar } from '@shared/components/ListToolbar';
import { BranchLifecycleDialog } from './BranchLifecycleDialog';

interface BranchManagerProps {
  tenantId: string;
}

interface BranchDraft {
  name: string;
  code: string;
  geofencingMetadata: string;
}

type BranchTranslations = ReturnType<typeof useI18n>;

/**
 * Los tres estados se pintan distinto a propósito: «Inactiva» es una pausa que se deshace y
 * «Cerrada» no. Confundirlas es el error caro de esta pantalla (ADR-0164).
 */
const BRANCH_BADGE: Record<
  'Active' | 'Inactive' | 'Closed',
  { status: string; label: (t: BranchTranslations) => string }
> = {
  Active: { status: 'Active', label: t => t.active },
  Inactive: { status: 'Pending', label: t => t.suspended },
  Closed: { status: 'Suspended', label: t => t.branchClosed },
};

export const BranchManager: React.FC<BranchManagerProps> = ({ tenantId }) => {
  // ── View controls ─────────────────────────────────────────────────────────
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');
  const [activeFilter, setActiveFilter] = useState('all');
  const [sortBy, setSortBy] = useState('name');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc');

  // ADR-0164: las cerradas ya no vienen en el listado; solo se piden cuando se consultan a
  // propósito. Por eso el filtro «Cerradas» no filtra en cliente: cambia la consulta.
  const showClosed = activeFilter === 'closed';

  const {
    data: branches = [],
    isLoading,
    refetch,
    isFetching,
  } = useGetBranches(tenantId, showClosed);
  const addBranchMutation = useAddBranch(tenantId);
  const updateBranchMutation = useUpdateBranch(tenantId);
  const closeBranchMutation = useCloseBranch(tenantId);
  const deactivateBranchMutation = useDeactivateBranch(tenantId);
  const reactivateBranchMutation = useReactivateBranch(tenantId);
  const t = useI18n();

  // ── Add form ──────────────────────────────────────────────────────────────
  const [isAdding, setIsAdding] = useState(false);
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [geofencing, setGeofencing] = useState('');
  const [errorMsg, setErrorMsg] = useState('');

  // ── Cierre definitivo y bitácora ──────────────────────────────────────────
  const [branchToClose, setBranchToClose] = useState<Branch | null>(null);
  const [closeReason, setCloseReason] = useState('');
  const [branchInLifecycle, setBranchInLifecycle] = useState<Branch | null>(null);

  // Estable a propósito: `M3Dialog` la usa como `onEscape` de su trampa de foco, y una identidad
  // nueva en cada render vuelve a enfocar el primer botón del diálogo, expulsando el cursor del
  // campo de motivo en cuanto se teclea (y un espacio acabaría pulsando ese botón).
  const cancelClose = useCallback(() => {
    setBranchToClose(null);
    setCloseReason('');
  }, []);

  const confirmClose = async () => {
    if (!branchToClose) return;
    try {
      await closeBranchMutation.mutateAsync({
        branchId: branchToClose.branchId,
        reason: closeReason,
      });
    } catch {
      // El hook ya notifica, y con el desglose de lo que bloquea si el backend respondió 409.
    }
    cancelClose();
  };

  // ── Inline edit ───────────────────────────────────────────────────────────
  const edit = useInlineEdit<BranchDraft>(['name', 'code', 'geofencingMetadata']);

  const openBranchEdit = (b: {
    branchId: string;
    name: string;
    code: string;
    geofencingMetadata?: string | null;
  }) => {
    edit.openEdit(b.branchId, {
      name: b.name,
      code: b.code,
      geofencingMetadata: b.geofencingMetadata ?? '',
    });
  };

  const saveBranchEdit = () => {
    const result = edit.commitEdit();
    if (!result) return;
    const name = (result.draft.name ?? '').trim();
    if (!name) return;
    // Persist to the backend (the mutation owns the success/error notification).
    updateBranchMutation.mutate({
      branchId: result.id,
      name,
      geofencingMetadata: (result.draft.geofencingMetadata ?? '').trim() || null,
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg('');

    if (code.length < 3) {
      setErrorMsg(t.branchCodeHelper);
      return;
    }
    if (name.length < 3) {
      setErrorMsg(t.branchNameHelper);
      return;
    }

    try {
      await addBranchMutation.mutateAsync({
        code: formatSystemCode(code),
        name,
        geofencingMetadata: geofencing || undefined,
      });
      setCode('');
      setName('');
      setGeofencing('');
      setIsAdding(false);
    } catch {
      // Handled in mutation hook
    }
  };

  // «Cerradas» ya viene resuelto por la consulta; el resto se filtra en cliente sobre las abiertas.
  let filteredBranches = branches;
  if (activeFilter === 'active' || activeFilter === 'inactive') {
    filteredBranches = filteredBranches.filter(b =>
      activeFilter === 'active' ? branchState(b) === 'Active' : branchState(b) === 'Inactive'
    );
  } else if (showClosed) {
    filteredBranches = filteredBranches.filter(b => branchState(b) === 'Closed');
  }
  filteredBranches = [...filteredBranches].sort((a, b) => {
    let cmp = 0;
    if (sortBy === 'name') cmp = a.name.localeCompare(b.name);
    else if (sortBy === 'code') cmp = a.code.localeCompare(b.code);
    return sortOrder === 'asc' ? cmp : -cmp;
  });

  return (
    <div className="space-y-4 select-none">
      <div className="flex items-center justify-between">
        <SectionHeader
          title={t.subLocations}
          subtitle={t.subLocationsSubtitle}
          actions={
            <IconButton
              tooltip={t.refreshBranches}
              onClick={() => refetch()}
              className={isFetching ? 'animate-spin' : ''}
            >
              <RefreshCw className="w-3.5 h-3.5" />
            </IconButton>
          }
        />
      </div>

      <ListToolbar
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        filterOptions={[
          { label: t.filterOpenBranches, value: 'all' },
          { label: 'Activas', value: 'active' },
          { label: 'Inactivas', value: 'inactive' },
          { label: t.filterClosedBranches, value: 'closed' },
        ]}
        activeFilter={activeFilter}
        onFilterChange={setActiveFilter}
        sortOptions={[
          { label: 'Nombre', value: 'name' },
          { label: 'Código', value: 'code' },
        ]}
        sortBy={sortBy}
        onSortByChange={setSortBy}
        sortOrder={sortOrder}
        onSortOrderToggle={() => setSortOrder(o => (o === 'asc' ? 'desc' : 'asc'))}
        itemCount={branches.length}
        itemLabel="sucursal"
        onAdd={() => setIsAdding(true)}
      />

      <div className="grid grid-cols-1 gap-4">
        <InlineAddForm
          isOpen={isAdding}
          onToggle={open => {
            setIsAdding(open);
            if (!open) setErrorMsg('');
          }}
          onSubmit={handleSubmit}
          addLabel="+"
          title={t.addingLocation}
          cancelLabel={t.cancelEdit}
          submitLabel={t.saveLocation}
          isLoading={addBranchMutation.isPending}
          triggerEmphasis="none"
          error={errorMsg || undefined}
        >
          <M3TextField
            label={t.branchCode}
            required
            value={code}
            onChange={e => setCode(e.target.value)}
            placeholder="e.g. SUC_MIRAFLORES"
          />
          <M3TextField
            label={t.branchName}
            required
            value={name}
            onChange={e => setName(e.target.value)}
            placeholder="e.g. Sucursal Miraflores"
          />
          <M3TextField
            label={t.geofencingMeta}
            value={geofencing}
            onChange={e => setGeofencing(e.target.value)}
            placeholder="e.g. -12.1191,-77.0291;r=500"
          />
        </InlineAddForm>

        <div className="space-y-2.5 pr-1">
          {isLoading ? (
            <div className="py-8 text-center text-[10px] text-m3-secondary flex flex-col items-center justify-center gap-1.5">
              <RefreshCw className="w-5 h-5 animate-spin text-m3-primary" />
              {t.loadingProfile}
            </div>
          ) : branches.length === 0 ? (
            <EmptyState
              icon={<MapPin className="w-6 h-6 text-m3-outline" />}
              message={t.noBranches}
            />
          ) : filteredBranches.length === 0 ? (
            <EmptyState
              icon={<MapPin className="w-6 h-6 text-m3-outline" />}
              message="No hay sucursales que coincidan con el filtro"
            />
          ) : (
            filteredBranches.map(b =>
              edit.isEditing(b.branchId) ? (
                /* ── Inline edit form ── */
                <div
                  key={b.branchId}
                  className="p-3 rounded-xl border border-m3-primary/40 bg-m3-surface-container/60 animate-fadeIn"
                >
                  <div className="flex items-center justify-between mb-2.5 pb-1.5 border-b border-m3-outline/10">
                    <span className="text-[10px] font-semibold uppercase tracking-wider text-m3-primary flex items-center gap-1">
                      <Pencil className="w-2.5 h-2.5" /> {t.editBranch}
                    </span>
                    <button
                      type="button"
                      onClick={edit.cancelEdit}
                      className="p-0.5 rounded text-m3-secondary/60 hover:text-m3-primary hover:bg-m3-primary/10 transition-colors"
                    >
                      <X className="w-3 h-3" />
                    </button>
                  </div>

                  <div className="space-y-3">
                    <M3TextField
                      label={t.branchName}
                      value={edit.draft.name ?? ''}
                      onChange={e => edit.setField('name', e.target.value)}
                      compact
                    />
                    <M3TextField
                      label={t.branchCode}
                      value={edit.draft.code ?? ''}
                      onChange={e => edit.setField('code', e.target.value)}
                      compact
                    />
                    <M3TextField
                      label={t.geofencingMeta}
                      value={edit.draft.geofencingMetadata ?? ''}
                      onChange={e => edit.setField('geofencingMetadata', e.target.value)}
                      placeholder="e.g. -12.1191,-77.0291;r=500"
                      compact
                    />
                  </div>

                  <div className="flex justify-end gap-2 mt-2.5 pt-2 border-t border-m3-outline/10">
                    <button
                      type="button"
                      onClick={saveBranchEdit}
                      className="h-7 px-4 rounded-full bg-m3-primary text-white text-[10px] font-medium flex items-center justify-center gap-1.5 hover:bg-m3-primary/90 transition-colors"
                    >
                      <Save className="w-2.5 h-2.5" /> {t.saveBtn}
                    </button>
                    <button
                      type="button"
                      onClick={edit.cancelEdit}
                      className="h-7 px-3 rounded-full border border-m3-outline/30 text-m3-secondary text-[10px] font-medium hover:bg-m3-surface-variant transition-colors"
                    >
                      {t.cancelEdit}
                    </button>
                  </div>
                </div>
              ) : (
                /* ── Read-only row ── */
                <EntityRow
                  key={b.branchId}
                  id={b.branchId}
                  isActive={branchState(b) === 'Active'}
                  onDoubleClick={() => branchState(b) !== 'Closed' && openBranchEdit(b)}
                  trailing={
                    <>
                      {/* Una sucursal cerrada no admite ninguna transición: solo se consulta. */}
                      {branchState(b) !== 'Closed' && (
                        <IconButton
                          tooltip={t.editBtn}
                          onClick={() => openBranchEdit(b)}
                          className="opacity-0 group-hover/row:opacity-100"
                        >
                          <Pencil className="w-3.5 h-3.5" />
                        </IconButton>
                      )}
                      {branchState(b) === 'Active' && (
                        <IconButton
                          tooltip={t.deactivate}
                          onClick={() => deactivateBranchMutation.mutate(b.branchId)}
                          disabled={deactivateBranchMutation.isPending}
                        >
                          <EyeOff className="w-3.5 h-3.5" />
                        </IconButton>
                      )}
                      {branchState(b) === 'Inactive' && (
                        <IconButton
                          tooltip={t.reactivate}
                          onClick={() => reactivateBranchMutation.mutate(b.branchId)}
                          disabled={reactivateBranchMutation.isPending}
                        >
                          <ShieldCheck className="w-3.5 h-3.5" />
                        </IconButton>
                      )}
                      <IconButton
                        tooltip={t.branchLifecycle}
                        onClick={() => setBranchInLifecycle(b)}
                      >
                        <History className="w-3.5 h-3.5" />
                      </IconButton>
                      {/* Candado, no papelera: no borra nada, clausura. Y pide confirmación. */}
                      {branchState(b) !== 'Closed' && (
                        <IconButton
                          tooltip={t.closeBranch}
                          onClick={() => setBranchToClose(b)}
                          disabled={closeBranchMutation.isPending}
                          className="hover:text-m3-error hover:bg-m3-error/10"
                        >
                          <Lock className="w-3.5 h-3.5" />
                        </IconButton>
                      )}
                    </>
                  }
                >
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-[11px] text-m3-on-surface flex-1 min-w-0 truncate">
                      {b.name}
                    </span>
                    <div className="flex items-center gap-1.5 flex-shrink-0">
                      <CodeBadge code={b.code} size="xs" />
                      <StatusBadge
                        status={BRANCH_BADGE[branchState(b)].status}
                        label={BRANCH_BADGE[branchState(b)].label(t)}
                        className="text-[8px]"
                      />
                    </div>
                  </div>
                  {branchState(b) === 'Closed' && (
                    <p className="text-[9px] text-m3-error font-semibold flex items-center gap-0.5">
                      <Lock className="w-3 h-3" /> {t.branchClosedHint}
                    </p>
                  )}
                  {b.geofencingMetadata && (
                    <p
                      className="text-[9px] text-indigo-400 font-bold flex items-center gap-0.5 truncate"
                      title={b.geofencingMetadata}
                    >
                      <MapPin className="w-3 h-3 text-indigo-400" /> {b.geofencingMetadata}
                    </p>
                  )}
                </EntityRow>
              )
            )
          )}
        </div>
      </div>

      {/* Cerrar es irreversible, así que no se dispara desde un icono sin más: se confirma
          nombrando la sucursal y su código, y se puede dejar el motivo en la bitácora. */}
      <M3Dialog
        open={!!branchToClose}
        title={t.closeBranchTitle}
        iconColor="bg-m3-error/15 text-m3-error"
        onScrimClick={cancelClose}
        actions={[
          { label: t.cancelEdit, variant: 'outlined', onClick: cancelClose },
          {
            label: t.closeBranchConfirm,
            variant: 'filled',
            className: 'bg-m3-error hover:bg-m3-error/90 text-m3-on-error border-0',
            onClick: confirmClose,
          },
        ]}
      >
        <div className="space-y-3">
          <p className="text-xs text-m3-secondary leading-relaxed">
            {branchToClose && t.closeBranchMessage(branchToClose.name, branchToClose.code)}
          </p>
          <M3TextField
            label={t.closeBranchReason}
            value={closeReason}
            onChange={e => setCloseReason(e.target.value)}
            compact
          />
        </div>
      </M3Dialog>

      <BranchLifecycleDialog
        tenantId={tenantId}
        branch={branchInLifecycle}
        onClose={() => setBranchInLifecycle(null)}
      />
    </div>
  );
};
