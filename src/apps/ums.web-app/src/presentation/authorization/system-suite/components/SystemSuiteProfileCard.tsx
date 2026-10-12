import React from 'react';
import { useSetSystemSuiteStatus } from '@app/authorization/hooks/use-system-suite';
import { useI18n } from '@app/i18n/use-i18n';
import { useStatusLabel } from '@app/hooks/use-status-label';
import { useInlineEdit } from '@app/hooks/use-inline-edit';
import { useResetOnChange } from '@app/hooks/use-reset-on-change';
import { useNotificationStore } from '@app/stores/notification.store';
import { SystemSuite } from '@domain/authorization/models/system-suite.model';
import { Box, Sliders, ShieldAlert, CheckCircle2, Pencil, Save, X, Wrench } from 'lucide-react';
import { M3Card } from '@shared/components/M3Card';
import { M3Button } from '@shared/components/M3Button';
import { M3TextField } from '@shared/components/M3TextField';
import { M3Dialog } from '@shared/components/M3Dialog';
import { StatusBadge } from '@shared/components/StatusBadge';
import { CodeBadge } from '@shared/components/CodeBadge';
import { IconButton } from '@shared/components/Tooltip';
import { KeyValueRow } from '@shared/components/KeyValueRow';
import { SectionHeader } from '@shared/components/SectionHeader';

interface SystemSuiteProfileCardProps {
  systemSuite: SystemSuite;
  onSystemSuiteUpdate: (systemSuiteId: string, patch: Partial<SystemSuite>) => void;
  onEditingChange?: (isEditing: boolean) => void;
}

interface SystemSuiteDraft {
  name: string;
  description: string;
}

export const SystemSuiteProfileCard: React.FC<SystemSuiteProfileCardProps> = ({
  systemSuite,
  onSystemSuiteUpdate,
  onEditingChange,
}) => {
  const t = useI18n();
  const statusLabel = useStatusLabel();
  const addNotification = useNotificationStore(s => s.addNotification);

  const setStatusMutation = useSetSystemSuiteStatus(systemSuite.systemSuiteId);
  const isPendingMutation = setStatusMutation.isPending;

  const edit = useInlineEdit<SystemSuiteDraft>(['name', 'description']);
  const [showDiscardDialog, setShowDiscardDialog] = React.useState(false);

  useResetOnChange(systemSuite.systemSuiteId, () => {
    edit.cancelEdit();
    onEditingChange?.(false);
  });

  const openEdit = () => {
    edit.openEdit(systemSuite.systemSuiteId, {
      name: systemSuite.name,
      description: systemSuite.description,
    });
    onEditingChange?.(true);
  };

  const cancelEdit = () => {
    edit.cancelEdit();
    onEditingChange?.(false);
  };

  const saveEdit = () => {
    const name = (edit.draft.name ?? '').trim();
    if (!name) return;

    const result = edit.commitEdit();
    if (!result) return;

    onSystemSuiteUpdate(systemSuite.systemSuiteId, {
      name,
      description: (result.draft.description ?? '').trim(),
    });
    addNotification({
      title: t.notifSystemSuiteUpdated,
      message: t.notifSystemSuiteUpdatedMsg(name),
      type: 'success',
    });
    onEditingChange?.(false);
  };

  const handleToggleStatus = (newStatus: 'Active' | 'Maintenance' | 'Deprecated') => {
    // La mutación (useNotifiedMutation) es dueña de la notificación success/error e invalida
    // el query. El estado local se refleja sólo en onSuccess: si el backend rechaza el cambio,
    // no se deja el botón en un estado falso ni se dispara un aviso prematuro (bug corregido, G-129).
    setStatusMutation.mutate(newStatus, {
      onSuccess: () => onSystemSuiteUpdate(systemSuite.systemSuiteId, { status: newStatus }),
    });
  };

  const renderActions = () => {
    switch (systemSuite.status) {
      case 'Active':
        return (
          <button
            type="button"
            onClick={() => handleToggleStatus('Maintenance')}
            disabled={isPendingMutation}
            className="h-7 px-3 rounded-full border border-amber-500/30 text-amber-500 text-[11px] font-medium flex items-center gap-1.5 hover:bg-amber-500/10 transition-colors disabled:opacity-50"
          >
            <Wrench className="w-3 h-3" /> {t.maintenanceBtn}
          </button>
        );
      case 'Maintenance':
        return (
          <button
            type="button"
            onClick={() => handleToggleStatus('Active')}
            disabled={isPendingMutation}
            className="h-7 px-3 rounded-full bg-emerald-600 hover:bg-emerald-500 text-white text-[11px] font-medium flex items-center gap-1.5 transition-colors disabled:opacity-50"
          >
            <CheckCircle2 className="w-3 h-3" /> {t.activateBtn}
          </button>
        );
      case 'Deprecated':
        return (
          <button
            type="button"
            onClick={() => handleToggleStatus('Active')}
            disabled={isPendingMutation}
            className="h-7 px-3 rounded-full border border-emerald-500/30 text-emerald-500 text-[11px] font-medium flex items-center gap-1.5 hover:bg-emerald-500/10 transition-colors disabled:opacity-50"
          >
            <CheckCircle2 className="w-3 h-3" /> {t.activateBtn}
          </button>
        );
      default:
        return null;
    }
  };

  return (
    <>
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
              cancelEdit();
            },
          },
        ]}
      />

      <M3Card
        variant="elevated"
        className="p-5 border border-m3-outline/25 bg-m3-surface-container/20 shadow-sm group"
        onDoubleClick={() => !edit.hasEditing && openEdit()}
      >
        {!edit.hasEditing ? (
          <div className="space-y-4">
            <SectionHeader
              title={systemSuite.name}
              subtitle={systemSuite.code}
              badge={<CodeBadge code={systemSuite.status} />}
              actions={
                <div className="flex items-center gap-2">
                  {renderActions()}
                  <IconButton
                    tooltip={t.editBtn}
                    onClick={openEdit}
                    className="opacity-0 group-hover:opacity-100"
                  >
                    <Pencil className="w-3.5 h-3.5" />
                  </IconButton>
                </div>
              }
            />

            <div className="space-y-3 text-xs">
              <KeyValueRow
                icon={<Box className="w-3.5 h-3.5" />}
                label={t.systemSuiteCode}
                value={
                  <span className="font-mono text-m3-on-surface text-xs">{systemSuite.code}</span>
                }
              />
              <KeyValueRow
                icon={<Sliders className="w-3.5 h-3.5" />}
                label={t.status}
                value={
                  <StatusBadge
                    status={systemSuite.status}
                    label={statusLabel(systemSuite.status)}
                  />
                }
              />
              {systemSuite.description && (
                <KeyValueRow
                  icon={<Wrench className="w-3.5 h-3.5" />}
                  label={t.description}
                  value={
                    <span className="text-m3-on-surface text-xs">{systemSuite.description}</span>
                  }
                />
              )}
            </div>

            <div className="flex items-center justify-between text-[11px] pt-2 border-t border-m3-outline/15">
              <div className="flex items-center gap-1.5 text-m3-secondary font-medium">
                <ShieldAlert className="w-3.5 h-3.5" />
                <span>{t.stateControls}</span>
              </div>
              <div className="flex items-center gap-2">
                {systemSuite.status !== 'Deprecated' && (
                  <button
                    type="button"
                    onClick={() => handleToggleStatus('Deprecated')}
                    disabled={isPendingMutation}
                    className="h-7 px-3 rounded-full text-rose-500 text-[11px] font-medium hover:bg-rose-500/10 transition-colors disabled:opacity-50"
                  >
                    {t.deprecateBtn}
                  </button>
                )}
              </div>
            </div>

            <p className="text-xs text-m3-secondary/50 mt-3 text-center">{t.doubleClickToEdit}</p>
          </div>
        ) : (
          <div className="space-y-0 animate-fadeIn">
            <div className="flex items-center justify-between mb-4">
              <span className="text-sm font-medium text-m3-primary flex items-center gap-1.5">
                <Pencil className="w-3.5 h-3.5" /> {t.editSystemSuite}
              </span>
              <IconButton tooltip={t.cancelEdit} onClick={cancelEdit}>
                <X className="w-3.5 h-3.5" />
              </IconButton>
            </div>

            <M3TextField
              label={t.systemSuiteName}
              required
              value={edit.draft.name ?? ''}
              onChange={e => edit.setField('name', e.target.value)}
            />
            <M3TextField
              label={t.description}
              value={edit.draft.description ?? ''}
              onChange={e => edit.setField('description', e.target.value)}
            />

            <div className="flex gap-2 pt-1">
              <M3Button
                variant="filled"
                onClick={saveEdit}
                className="flex-1 flex items-center justify-center gap-1.5"
              >
                <Save className="w-3.5 h-3.5" /> {t.saveBtn}
              </M3Button>
              <M3Button variant="outlined" onClick={cancelEdit} className="flex-1">
                {t.cancelEdit}
              </M3Button>
            </div>
          </div>
        )}
      </M3Card>
    </>
  );
};
