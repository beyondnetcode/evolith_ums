/**
 * AppConfigurationDetailPanel
 */
import React from 'react';
import { Settings, Lock, Link, Clock, ShieldCheck, Edit2, Trash2 } from 'lucide-react';
import type { AppConfiguration } from '@domain/configuration/schemas/app-configuration.schema';
import { useI18n } from '@app/i18n/use-i18n';
import { DetailPanelShell } from '@shared/components/DetailPanelShell';
import { StatusBadge } from '@shared/components/StatusBadge';
import { CodeBadge } from '@shared/components/CodeBadge';
import { M3Button } from '@shared/components/M3Button';
import { SmartConfigInput } from '@presentation/shared/components/SmartConfigInput';
import { IconButton } from '@shared/components/Tooltip';
import { DetailSection } from '@shared/components/DetailSection';
import { appConfigurationService } from '@infra/configuration/services/app-configuration.service';
import {
  usePublishAppConfiguration,
  useArchiveAppConfiguration,
} from '@app/configuration/hooks/use-app-configuration';
import { useNotificationStore } from '@app/stores/notification.store';
import { AppConfigurationAuditTrailPanel } from './AppConfigurationAuditTrailPanel';

interface AppConfigurationDetailPanelProps {
  config?: AppConfiguration;
  onEdit?: () => void;
  onDelete?: () => void;
  triggerEditValue?: boolean;
}

export function AppConfigurationDetailPanel({
  config,
  onEdit,
  onDelete,
  triggerEditValue,
}: AppConfigurationDetailPanelProps): React.JSX.Element {
  const t = useI18n();
  const addNotification = useNotificationStore(s => s.addNotification);
  const publishMutation = usePublishAppConfiguration();
  const archiveMutation = useArchiveAppConfiguration();

  const [isEditingValue, setIsEditingValue] = React.useState(false);
  const [editValue, setEditValue] = React.useState('');

  React.useEffect(() => {
    if (triggerEditValue && config) {
      setEditValue(config.value);
      setIsEditingValue(true);
    }
  }, [triggerEditValue, config]);

  const handleStartEditValue = () => {
    if (config) {
      setEditValue(config.value);
      setIsEditingValue(true);
    }
  };

  const handleSaveValue = async () => {
    if (!config) return;
    try {
      await appConfigurationService.updateAppConfiguration(config.appConfigurationId, {
        value: editValue,
        description: config.description || '',
      });
      addNotification({
        title: t.success ?? 'Success',
        message: t.parameterUpdated ?? 'Parameter value updated',
        type: 'success',
      });
      setIsEditingValue(false);
    } catch {
      addNotification({
        title: t.error ?? 'Error',
        message: t.parameterUpdateFailed ?? 'Failed to update parameter value',
        type: 'error',
      });
    }
  };

  const handleCancelEditValue = () => {
    setIsEditingValue(false);
    setEditValue('');
  };

  const handlePublish = async () => {
    if (!config) return;
    try {
      await publishMutation.mutateAsync(config.appConfigurationId);
      addNotification({
        title: t.parameterPublished ?? 'Parameter Published',
        message: `${config.code} is now active`,
        type: 'success',
      });
    } catch {
      addNotification({
        title: t.publishFailed ?? 'Publish Failed',
        message: t.publishFailedMsg ?? 'Could not publish the parameter',
        type: 'error',
      });
    }
  };

  const handleArchive = async () => {
    if (!config) return;
    try {
      await archiveMutation.mutateAsync(config.appConfigurationId);
      addNotification({
        title: t.parameterArchived ?? 'Parameter Archived',
        message: `${config.code} has been archived`,
        type: 'warning',
      });
    } catch {
      addNotification({
        title: t.archiveFailed ?? 'Archive Failed',
        message: t.archiveFailedMsg ?? 'Could not archive the parameter',
        type: 'error',
      });
    }
  };

  const isLoading = publishMutation.isPending || archiveMutation.isPending;

  if (!config) {
    return (
      <DetailPanelShell
        isEmpty={true}
        emptyLabel={t.selectParameter ?? 'Select a parameter to view details'}
        entityKey=""
      />
    );
  }

  return (
    <DetailPanelShell
      isLoading={false}
      isEmpty={false}
      entityKey={config.appConfigurationId}
      header={
        <div className="flex items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <div className="p-2 rounded-lg bg-m3-surface-container">
              <Settings className="w-5 h-5 text-m3-primary" />
            </div>
            <div className="min-w-0">
              <h2 className="text-[12px] font-medium text-m3-on-surface truncate">{config.code}</h2>
              <div className="flex items-center gap-2 mt-1">
                <CodeBadge code={config.scope} size="xs" />
                <StatusBadge status={config.status} label={config.status} size="xs" />
                {config.isEncrypted && <Lock className="w-4 h-4 text-amber-500" />}
              </div>
            </div>
          </div>
          <div className="flex items-center gap-1">
            {onEdit && (
              <IconButton tooltip={t.editBtn ?? 'Edit'} onClick={onEdit}>
                <Edit2 className="w-4 h-4" />
              </IconButton>
            )}
            {onDelete && (
              <IconButton
                tooltip={t.deleteBtn ?? 'Delete'}
                onClick={onDelete}
                className="hover:text-rose-500 hover:bg-rose-500/10"
              >
                <Trash2 className="w-4 h-4" />
              </IconButton>
            )}
          </div>
        </div>
      }
    >
      <div className="space-y-4 p-4">
        {/* Description */}
        <p className="text-[12px] text-m3-secondary">{config.description || '-'}</p>

        {/* Value */}
        <DetailSection
          title={t.value ?? 'Value'}
          noPadding
          content={
            config.isEncrypted ? (
              <div className="flex items-center gap-2 text-amber-600">
                <Lock className="w-4 h-4" />
                <span className="text-[10px]">[Encrypted]</span>
              </div>
            ) : isEditingValue ? (
              <div className="flex items-center gap-2">
                <SmartConfigInput code={config.code} value={editValue} onChange={setEditValue} />
                <M3Button variant="filled" size="sm" onClick={handleSaveValue}>
                  {t.save ?? 'Save'}
                </M3Button>
                <M3Button variant="text" size="sm" onClick={handleCancelEditValue}>
                  {t.cancel ?? 'Cancel'}
                </M3Button>
              </div>
            ) : (
              <div
                className="inline-flex items-center gap-2 bg-m3-surface-container px-3 py-2 rounded-lg cursor-pointer hover:bg-m3-surface-container-low transition-colors"
                onClick={handleStartEditValue}
              >
                <span className="text-[12px] text-m3-on-surface truncate w-48">{config.value}</span>
                <Edit2 className="w-4 h-4 text-m3-outline flex-shrink-0" />
              </div>
            )
          }
        />

        {/* Metadata */}
        <div className="flex gap-4 text-[11px]">
          <div className="flex items-center gap-1.5">
            <Clock className="w-4 h-4 text-m3-secondary" />
            <span className="text-m3-secondary">v</span>
            <span className="font-medium text-m3-on-surface">{config.version}</span>
          </div>
          <div className="flex items-center gap-1.5">
            {config.isInheritable ? (
              <>
                <Link className="w-4 h-4 text-emerald-500" />
                <span className="text-emerald-600 font-medium">Inheritable</span>
              </>
            ) : (
              <>
                <ShieldCheck className="w-4 h-4 text-m3-outline" />
                <span className="text-m3-secondary">Local</span>
              </>
            )}
          </div>
        </div>

        {/* Tenant/Suite/Module info */}
        {(config.tenantId || config.systemSuiteId || config.moduleId) && (
          <DetailSection
            title={t.scopeDetails ?? 'Scope Details'}
            noPadding
            content={
              <div className="flex flex-wrap gap-2">
                {config.tenantId && (
                  <CodeBadge code={`Tenant: ${config.tenantId.substring(0, 8)}...`} size="sm" />
                )}
                {config.systemSuiteId && (
                  <CodeBadge code={`Suite: ${config.systemSuiteId.substring(0, 8)}...`} size="sm" />
                )}
                {config.moduleId && (
                  <CodeBadge code={`Module: ${config.moduleId.substring(0, 8)}...`} size="sm" />
                )}
              </div>
            }
          />
        )}

        <AppConfigurationAuditTrailPanel config={config} />

        {/* Actions */}
        <div className="flex gap-2 pt-2 border-t border-m3-outline/10">
          {config.status === 'Draft' && (
            <M3Button variant="filled" size="sm" onClick={handlePublish} disabled={isLoading}>
              {t.publish ?? 'Publish'}
            </M3Button>
          )}
          {config.status === 'Published' && (
            <M3Button variant="outlined" size="sm" onClick={handleArchive} disabled={isLoading}>
              {t.archive ?? 'Archive'}
            </M3Button>
          )}
        </div>
      </div>
    </DetailPanelShell>
  );
}
