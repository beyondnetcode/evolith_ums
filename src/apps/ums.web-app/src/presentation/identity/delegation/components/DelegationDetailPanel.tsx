import React from 'react';
import { Shield, Key, Calendar, AlertCircle } from 'lucide-react';
import { Delegation } from '@domain/identity/models/delegation.model';
import { DetailPanelShell, DetailTab, DetailSection } from '@shared/components';
import { DelegationProfileCard } from './DelegationProfileCard';
import { useI18n } from '@app/i18n/use-i18n';
import { KeyValueRow } from '@shared/components/KeyValueRow';
import { CodeBadge } from '@shared/components/CodeBadge';
import { useDateFormat } from '@app/formatting/use-date-format';

export type DelegationConsoleTab = 'overview' | 'permissions';

interface DelegationDetailPanelProps {
  selectedId: string;
  activeDelegation: Delegation | undefined;
  isLoading: boolean;
  activeConsoleTab: DelegationConsoleTab;
  consoleTabs: DelegationConsoleTab[];
  onConsoleTabChange: (tab: DelegationConsoleTab) => void;
}

export const DelegationDetailPanel: React.FC<DelegationDetailPanelProps> = ({
  selectedId,
  activeDelegation,
  isLoading,
  activeConsoleTab,
  consoleTabs,
  onConsoleTabChange,
}) => {
  const t = useI18n();
  const { formatDateTime } = useDateFormat();

  const icons: Record<DelegationConsoleTab, React.ReactNode> = {
    overview: <Shield className="w-3.5 h-3.5" />,
    permissions: <Key className="w-3.5 h-3.5" />,
  };

  const labels: Record<DelegationConsoleTab, string> = {
    overview: t.overview ?? 'Resumen',
    permissions: t.permissions ?? 'Permisos',
  };

  const tabs: DetailTab<DelegationConsoleTab>[] = consoleTabs.map(tab => ({
    key: tab,
    label: labels[tab],
    icon: icons[tab],
  }));

  return (
    <DetailPanelShell
      isLoading={isLoading}
      isEmpty={!activeDelegation}
      loadingLabel={t.loadingProfile ?? 'Loading...'}
      emptyLabel={'Select a delegation'}
      entityKey={selectedId}
      tabs={tabs}
      activeTab={activeConsoleTab}
      onTabChange={onConsoleTabChange}
      header={
        activeDelegation ? <DelegationProfileCard delegation={activeDelegation} /> : undefined
      }
    >
      <div className="p-4 space-y-4">
        {activeConsoleTab === 'overview' && activeDelegation && (
          <>
            <DetailSection
              title="Período de Validez"
              content={
                <div className="space-y-2">
                  <KeyValueRow
                    icon={<Calendar className="w-4 h-4" />}
                    label={t.validFrom ?? 'Válido desde'}
                    value={formatDateTime(activeDelegation.validFrom) ?? 'Indefinido'}
                  />
                  <KeyValueRow
                    icon={<Calendar className="w-4 h-4" />}
                    label={t.validUntil ?? 'Válido hasta'}
                    value={formatDateTime(activeDelegation.validUntil) ?? 'Indefinido'}
                  />
                  <KeyValueRow
                    icon={<AlertCircle className="w-4 h-4" />}
                    label={t.requiresApproval ?? 'Requiere Aprobación'}
                    value={activeDelegation.requiresApproval ? (t.yes ?? 'Sí') : (t.no ?? 'No')}
                    bordered={false}
                  />
                </div>
              }
            />
          </>
        )}

        {activeConsoleTab === 'permissions' && activeDelegation && (
          <DetailSection
            title={t.allowedActions ?? 'Acciones Permitidas'}
            content={
              <div className="flex flex-wrap gap-2">
                {activeDelegation.allowedActions.map(action => (
                  <CodeBadge key={action} code={action} />
                ))}
              </div>
            }
          />
        )}
      </div>
    </DetailPanelShell>
  );
};
