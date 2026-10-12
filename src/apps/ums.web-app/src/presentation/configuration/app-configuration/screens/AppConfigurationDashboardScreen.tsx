/**
 * AppConfigurationDashboardScreen
 *
 * Master-detail layout for the AppConfiguration aggregate:
 *  • Left panel  — paginated list with filter/sort controls
 *  • Right panel — detail view for the selected configuration
 *  • Modal       — parameter definition picker to link multiple configs at once
 */
import React, { useState } from 'react';
import { useAppConfigurationDashboard } from '@app/configuration/hooks/use-app-configuration-dashboard';
import {
  useCreateAppConfiguration,
  useDeleteAppConfiguration,
} from '@app/configuration/hooks/use-app-configuration';
import { AppConfigurationListPanel } from '../components/AppConfigurationListPanel';
import { AppConfigurationDetailPanel } from '../components/AppConfigurationDetailPanel';
import { ParameterDefinitionPickerDialog } from '../components/ParameterDefinitionPickerDialog';
import { PageShell } from '@shared/layouts/PageShell';
import { MasterDetailLayout } from '@shared/layouts/MasterDetailLayout';
import { M3Dialog } from '@shared/components/M3Dialog';
import { useNotificationStore } from '@app/stores/notification.store';
import { useI18n } from '@app/i18n/use-i18n';
import type { ParameterDefinition } from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';

export default function AppConfigurationDashboardScreen(): React.JSX.Element {
  const t = useI18n();
  const d = useAppConfigurationDashboard();
  const createMutation = useCreateAppConfiguration();
  const deleteMutation = useDeleteAppConfiguration();
  const addNotification = useNotificationStore(s => s.addNotification);
  const [isPickerOpen, setIsPickerOpen] = useState(false);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);
  const [triggerEditValue, setTriggerEditValue] = useState(false);

  const existingCodes = d.knownConfigs.map(c => c.code);

  const handleParameterSelect = async (parameters: ParameterDefinition[]) => {
    try {
      const results = await Promise.all(
        parameters.map(param =>
          createMutation.mutateAsync({
            code: param.code.toUpperCase(),
            value: param.defaultValue ?? '',
            description: param.description?.trim() || `Parameter: ${param.code}`,
            isInheritable: false,
            isEncrypted: false,
          })
        )
      );

      const count = results.length;
      addNotification({
        title: t.parametersLinked ?? 'Parameters Linked',
        message: `${count} parameter${count > 1 ? 's' : ''} added to configuration`,
        type: 'success',
      });

      d.handleCreateSuccess();
      if (results.length > 0) {
        d.setSelectedId(results[0].appConfigurationId);
      }
      setIsPickerOpen(false);
    } catch (err: any) {
      const errorMsg =
        err?.normalised?.message ||
        err?.response?.data?.detail ||
        err?.message ||
        t.failedToLinkParameter;
      console.error('Create config error:', err?.response?.data);
      addNotification({
        title: t.error ?? 'Error',
        message: errorMsg,
        type: 'error',
      });
    }
  };

  const handleDeleteRequest = (configId: string) => {
    setPendingDeleteId(configId);
    setIsDeleteDialogOpen(true);
  };

  const handleDeleteConfirm = async () => {
    if (!pendingDeleteId) return;
    try {
      await deleteMutation.mutateAsync(pendingDeleteId);
      addNotification({
        title: t.parameterDeleted ?? 'Parameter Deleted',
        message: t.parameterDeletedMsg ?? 'Configuration has been deleted',
        type: 'success',
      });
      setIsDeleteDialogOpen(false);
      setPendingDeleteId(null);
      if (d.selectedId === pendingDeleteId) {
        d.setSelectedId('');
      }
    } catch (err: any) {
      const errorMsg = err?.normalised?.message || err?.response?.data?.detail || t.deleteFailed;
      addNotification({
        title: t.error ?? 'Error',
        message: errorMsg,
        type: 'error',
      });
    }
  };

  return (
    <PageShell>
      <ParameterDefinitionPickerDialog
        isOpen={isPickerOpen}
        onClose={() => setIsPickerOpen(false)}
        onSelect={handleParameterSelect}
        existingCodes={existingCodes}
      />

      <M3Dialog
        open={isDeleteDialogOpen}
        title={t.deleteConfiguration ?? 'Delete Configuration'}
        message={
          t.deleteConfigurationConfirm ??
          'Are you sure you want to delete this configuration? This action cannot be undone.'
        }
        onScrimClick={() => setIsDeleteDialogOpen(false)}
        actions={[
          {
            label: t.cancelBtn ?? 'Cancel',
            variant: 'outlined',
            onClick: () => setIsDeleteDialogOpen(false),
          },
          {
            label: t.deleteBtn ?? 'Delete',
            variant: 'filled',
            className: 'bg-m3-error hover:bg-m3-error/90 border-0',
            onClick: handleDeleteConfirm,
          },
        ]}
      />

      <MasterDetailLayout
        splitterLabel="Resize configuration detail panel"
        master={
          <AppConfigurationListPanel
            configs={d.knownConfigs}
            selectedId={d.selectedId}
            isLoading={d.isLoadingList}
            error={d.listError}
            viewMode={d.viewMode}
            onViewModeChange={d.setViewMode}
            queryState={d.queryState}
            paginationState={{
              ...d.paginationState,
              totalItems: d.totalItems,
              totalPages: d.totalPages,
            }}
            onRegisterNew={() => setIsPickerOpen(true)}
            onSelectConfig={d.handleSelect}
            requiresFilter={d.requiresFilter}
          />
        }
        detail={
          d.activeConfig ? (
            <AppConfigurationDetailPanel
              config={d.activeConfig}
              onEdit={() => {
                setTriggerEditValue(true);
                setTimeout(() => setTriggerEditValue(false), 100);
              }}
              onDelete={() => handleDeleteRequest(d.activeConfig?.appConfigurationId || '')}
              triggerEditValue={triggerEditValue}
            />
          ) : (
            <AppConfigurationDetailPanel config={d.activeConfig} />
          )
        }
      />
    </PageShell>
  );
}
