import React from 'react';
import {
  useActivateTenant,
  useSuspendTenant,
  useSetManagementOwner,
  useUpdateTenant,
} from '@app/identity/hooks/use-tenant';
import { isTenantRoot, TenantRootBadge } from './tenant-list-renders';
import { useI18n } from '@app/i18n/use-i18n';
import { useStatusLabel } from '@app/hooks/use-status-label';
import { useInlineEdit } from '@app/hooks/use-inline-edit';
import { useResetOnChange } from '@app/hooks/use-reset-on-change';
import { Tenant } from '@domain/identity/models/tenant.model';
import { TENANT_TYPES } from '@domain/identity/constants/tenant.constants';
import {
  Building,
  Sliders,
  ShieldAlert,
  CheckCircle2,
  Pencil,
  Save,
  X,
  GitBranch,
  ShieldCheck,
} from 'lucide-react';
import { M3Card } from '@shared/components/M3Card';
import { M3Button } from '@shared/components/M3Button';
import { M3TextField } from '@shared/components/M3TextField';
import { M3Select } from '@shared/components/M3Select';
import { M3Dialog } from '@shared/components/M3Dialog';
import { StatusBadge } from '@shared/components/StatusBadge';
import { CodeBadge } from '@shared/components/CodeBadge';
import { IconButton } from '@shared/components/Tooltip';

// ─── Props ───────────────────────────────────────────────────────────────────

interface TenantProfileCardProps {
  tenant: Tenant;
  parentTenant: Tenant | null;
  onTenantUpdate: (tenantId: string, patch: Partial<Tenant>) => void;
  onEditingChange?: (isEditing: boolean) => void;
}

// ─── Inline-edit draft shape ────────────────────────────────────────────────

interface TenantDraft {
  name: string;
  code: string;
  companyReference: string;
  type: string;
}

// ─── Component ───────────────────────────────────────────────────────────────

export const TenantProfileCard: React.FC<TenantProfileCardProps> = ({
  tenant,
  parentTenant,
  onTenantUpdate,
  onEditingChange,
}) => {
  const t = useI18n();
  const statusLabel = useStatusLabel();

  const activateMutation = useActivateTenant(tenant.tenantId);
  const suspendMutation = useSuspendTenant(tenant.tenantId);
  const managementOwnerMutation = useSetManagementOwner(tenant.tenantId);
  const updateTenantMutation = useUpdateTenant();
  const isPendingMutation =
    activateMutation.isPending || suspendMutation.isPending || managementOwnerMutation.isPending;

  const edit = useInlineEdit<TenantDraft>(['name', 'code', 'companyReference', 'type']);
  const [showDiscardDialog, setShowDiscardDialog] = React.useState(false);

  // Reset editing state when the tenant changes
  useResetOnChange(tenant.tenantId, () => {
    edit.cancelEdit();
    onEditingChange?.(false);
  });

  const openTenantEdit = () => {
    edit.openEdit(tenant.tenantId, {
      name: tenant.name,
      code: tenant.code,
      companyReference: tenant.companyReference || '',
      type: tenant.type,
    });
    onEditingChange?.(true);
  };

  const cancelTenantEdit = () => {
    edit.cancelEdit();
    onEditingChange?.(false);
  };

  const saveTenantEdit = () => {
    const name = (edit.draft.name ?? '').trim();
    const code = (edit.draft.code ?? '').trim();
    if (!name || !code) return;

    const result = edit.commitEdit();
    if (!result) return;

    const companyReference = (result.draft.companyReference ?? '').trim();

    // Optimistic local override for immediate feedback
    onTenantUpdate(tenant.tenantId, {
      name,
      code: code.toUpperCase(),
      companyReference,
      type: result.draft.type,
    });
    // Persist to the backend (the mutation owns the success/error notification).
    // Code is immutable and is not part of the update payload.
    updateTenantMutation.mutate({
      tenantId: tenant.tenantId,
      name,
      type: result.draft.type ?? tenant.type,
      companyReference: companyReference || null,
    });
    onEditingChange?.(false);
  };

  const handleToggleStatus = (newStatus: 'Active' | 'Suspended') => {
    // La mutación (useNotifiedMutation) es dueña de la notificación success/error e invalida
    // el query. El estado local sólo se refleja SI el servidor confirma (onSuccess): así un
    // rechazo del backend —p. ej. TENANT_HAS_ACTIVE_USERS— no deja el botón en un estado
    // mentiroso ni dispara un aviso prematuro (bug corregido, G-129).
    const onSuccess = () => onTenantUpdate(tenant.tenantId, { status: newStatus });
    if (newStatus === 'Active') {
      activateMutation.mutate(undefined, { onSuccess });
    } else {
      suspendMutation.mutate(undefined, { onSuccess });
    }
  };

  return (
    <>
      {/* Discard-changes dialog */}
      <M3Dialog
        open={showDiscardDialog}
        title={t.unsavedChanges}
        message={t.unsavedChangesMsg}
        onScrimClick={() => setShowDiscardDialog(false)}
        actions={[
          { label: t.cancelEdit, variant: 'outlined', onClick: () => setShowDiscardDialog(false) },
          {
            label: t.discardChanges,
            variant: 'filled',
            className: 'bg-m3-error hover:bg-m3-error/90 border-0',
            onClick: () => {
              setShowDiscardDialog(false);
              cancelTenantEdit();
            },
          },
        ]}
      />

      {/* Profile card */}
      <M3Card
        variant="elevated"
        className="p-5 border border-m3-outline/25 bg-m3-surface-container/20 shadow-sm group"
        onDoubleClick={() => !edit.hasEditing && openTenantEdit()}
      >
        {!edit.hasEditing ? (
          <>
            <div className="flex justify-between items-start gap-4 pb-3.5 border-b border-m3-outline/15 mb-4">
              <div className="flex gap-3 flex-1 min-w-0">
                <div className="p-2 bg-m3-primary/10 rounded-lg text-m3-primary border border-m3-primary/10 self-start flex-shrink-0">
                  <Building className="w-5 h-5" />
                </div>
                <div className="min-w-0">
                  <h3 className="text-sm font-semibold text-m3-on-surface flex items-center gap-1.5 flex-wrap">
                    {tenant.name}
                    <CodeBadge code={tenant.code} />
                    {isTenantRoot(tenant) && <TenantRootBadge />}
                  </h3>
                  {tenant.companyReference && (
                    <p className="text-xs text-m3-secondary mt-0.5">{tenant.companyReference}</p>
                  )}
                  {parentTenant && (
                    <div className="flex items-center gap-1 mt-1">
                      <GitBranch className="w-3 h-3 text-m3-tertiary flex-shrink-0" />
                      <span className="text-[10px] text-m3-tertiary font-medium truncate">
                        {parentTenant.name}
                      </span>
                    </div>
                  )}
                </div>
              </div>
              <div className="flex items-center gap-2 flex-shrink-0">
                <StatusBadge status={tenant.status} label={statusLabel(tenant.status)} />
                <IconButton
                  tooltip={t.editTenant}
                  onClick={openTenantEdit}
                  className="opacity-0 group-hover:opacity-100"
                >
                  <Pencil className="w-3.5 h-3.5" />
                </IconButton>
              </div>
            </div>

            <div className="flex items-center justify-between text-[11px]">
              <div className="flex items-center gap-1.5 text-m3-secondary font-medium">
                <Sliders className="w-3.5 h-3.5" />
                <span>{t.stateControls}</span>
              </div>
              {tenant.status === 'Active' ? (
                <button
                  type="button"
                  onClick={() => handleToggleStatus('Suspended')}
                  disabled={isPendingMutation}
                  className="h-7 px-3 rounded-full border border-rose-500/30 text-rose-500 text-[11px] font-medium flex items-center gap-1.5 hover:bg-rose-500/10 transition-colors disabled:opacity-50"
                >
                  <ShieldAlert className="w-3 h-3" />
                  {t.suspendBtn}
                </button>
              ) : (
                <button
                  type="button"
                  onClick={() => handleToggleStatus('Active')}
                  disabled={isPendingMutation}
                  className="h-7 px-3 rounded-full bg-emerald-600 hover:bg-emerald-500 text-white text-[11px] font-medium flex items-center gap-1.5 transition-colors disabled:opacity-50"
                >
                  <CheckCircle2 className="w-3 h-3" />
                  {t.activateBtn}
                </button>
              )}
            </div>

            <div className="flex items-center justify-between text-[11px] mt-2">
              <div className="flex items-center gap-1.5 text-m3-secondary font-medium">
                <ShieldCheck className="w-3.5 h-3.5" />
                <span>UMS Management</span>
              </div>
              <button
                type="button"
                onClick={() => managementOwnerMutation.mutate(!tenant.isManagementOwner)}
                disabled={isPendingMutation}
                className={`h-7 px-3 rounded-full text-[11px] font-medium flex items-center gap-1.5 transition-colors disabled:opacity-50 ${
                  tenant.isManagementOwner
                    ? 'bg-m3-primary/10 border border-m3-primary/30 text-m3-primary hover:bg-m3-primary/20'
                    : 'border border-m3-outline/30 text-m3-secondary hover:bg-m3-surface-variant'
                }`}
              >
                <ShieldCheck className="w-3 h-3" />
                {tenant.isManagementOwner ? 'Owner' : 'Not owner'}
              </button>
            </div>

            <p className="text-xs text-m3-secondary/50 mt-3 text-center">{t.doubleClickToEdit}</p>
          </>
        ) : (
          <div className="animate-fadeIn">
            <div className="flex items-center justify-between mb-2.5 pb-1.5 border-b border-m3-outline/10">
              <span className="text-[10px] font-semibold uppercase tracking-wider text-m3-primary flex items-center gap-1">
                <Pencil className="w-2.5 h-2.5" /> {t.editTenant}
              </span>
              <button
                type="button"
                onClick={cancelTenantEdit}
                className="p-0.5 rounded text-m3-secondary/60 hover:text-m3-primary hover:bg-m3-primary/10 transition-colors"
              >
                <X className="w-3 h-3" />
              </button>
            </div>

            <div className="space-y-3">
              <M3TextField
                label={t.tenantName}
                required
                value={edit.draft.name ?? ''}
                onChange={e => edit.setField('name', e.target.value)}
                compact
              />
              <M3TextField
                label={t.tenantCode}
                required
                value={edit.draft.code ?? ''}
                onChange={e => edit.setField('code', e.target.value.toUpperCase())}
                compact
              />
              <M3TextField
                label={t.companyReference}
                value={edit.draft.companyReference ?? ''}
                onChange={e => edit.setField('companyReference', e.target.value)}
                compact
              />
              <M3Select
                label={t.tenantType}
                value={edit.draft.type ?? ''}
                onChange={e => edit.setField('type', e.target.value)}
                compact
              >
                {TENANT_TYPES.map(tp => (
                  <option key={tp} value={tp}>
                    {tp}
                  </option>
                ))}
              </M3Select>
            </div>

            <div className="flex justify-end gap-2 mt-2.5 pt-2 border-t border-m3-outline/10">
              <button
                type="button"
                onClick={saveTenantEdit}
                className="h-7 px-4 rounded-full bg-m3-primary text-white text-[10px] font-medium flex items-center justify-center gap-1.5 hover:bg-m3-primary/90 transition-colors"
              >
                <Save className="w-2.5 h-2.5" /> {t.saveBtn}
              </button>
              <button
                type="button"
                onClick={cancelTenantEdit}
                className="h-7 px-3 rounded-full border border-m3-outline/30 text-m3-secondary text-[10px] font-medium hover:bg-m3-surface-variant transition-colors"
              >
                {t.cancelEdit}
              </button>
            </div>
          </div>
        )}
      </M3Card>
    </>
  );
};
