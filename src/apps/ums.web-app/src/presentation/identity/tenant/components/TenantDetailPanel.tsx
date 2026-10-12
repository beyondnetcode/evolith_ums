import React from 'react';
import { MapPin, Key, GitBranch, Settings, History } from 'lucide-react';
import { Tenant } from '@domain/identity/models/tenant.model';
import { DetailPanelShell, DetailTab } from '@shared/components/DetailPanelShell';
import { BranchManager } from './BranchManager';
import { IdpPanel } from './IdpPanel';
import { TenantProfileCard } from './TenantProfileCard';
import { TenantConfigurationsPanel } from './TenantConfigurationsPanel';
import { TenantAuditPanel } from './TenantAuditPanel';
import { useI18n } from '@app/i18n/use-i18n';

export type ConsoleTab = 'branches' | 'providers' | 'configurations' | 'audit';

interface TenantDetailPanelProps {
  selectedId: string;
  activeTenant: Tenant | undefined;
  parentTenant: Tenant | null;
  isRootTenant: boolean;
  isLoading: boolean;
  activeConsoleTab: ConsoleTab;
  consoleTabs: ConsoleTab[];
  onConsoleTabChange: (tab: ConsoleTab) => void;
  onTenantUpdate: (tenantId: string, patch: Partial<Tenant>) => void;
  onTenantEditingChange: (isEditing: boolean) => void;
}

export const TenantDetailPanel: React.FC<TenantDetailPanelProps> = ({
  selectedId,
  activeTenant,
  parentTenant,
  isRootTenant,
  isLoading,
  activeConsoleTab,
  consoleTabs,
  onConsoleTabChange,
  onTenantUpdate,
  onTenantEditingChange,
}) => {
  const t = useI18n();

  const icons: Record<ConsoleTab, React.ReactNode> = {
    branches: <MapPin className="w-3.5 h-3.5" />,
    providers: <Key className="w-3.5 h-3.5" />,
    configurations: <Settings className="w-3.5 h-3.5" />,
    audit: <History className="w-3.5 h-3.5" />,
  };

  const labels: Record<ConsoleTab, string> = {
    branches: t.tabLocations,
    providers: t.tabAuthIdps,
    configurations: t.tabConfigurations,
    audit: t.tabAudit,
  };

  const tabs: DetailTab<ConsoleTab>[] = consoleTabs.map(tab => ({
    key: tab,
    label: labels[tab],
    icon: icons[tab],
  }));

  const banner = !isRootTenant ? (
    <div className="flex items-start gap-2.5 px-3.5 py-2.5 rounded-xl border border-m3-tertiary/25 bg-m3-tertiary/10 text-[11px] text-m3-on-surface-variant">
      <GitBranch className="w-3.5 h-3.5 text-m3-tertiary flex-shrink-0 mt-0.5" />
      <span>
        <span className="font-semibold text-m3-tertiary">Child tenant - </span>
        IdP configuration is managed by the parent organisation
        {parentTenant ? ` (${parentTenant.name})` : ''}.
      </span>
    </div>
  ) : undefined;

  return (
    <DetailPanelShell
      isLoading={isLoading}
      isEmpty={!activeTenant}
      loadingLabel={t.loadingProfile}
      emptyLabel={t.selectTenant}
      entityKey={selectedId}
      tabs={tabs}
      activeTab={activeConsoleTab}
      onTabChange={onConsoleTabChange}
      header={
        activeTenant ? (
          <TenantProfileCard
            tenant={activeTenant}
            parentTenant={parentTenant}
            onTenantUpdate={onTenantUpdate}
            onEditingChange={onTenantEditingChange}
          />
        ) : undefined
      }
      banner={banner}
    >
      {activeConsoleTab === 'branches' && activeTenant && (
        <BranchManager tenantId={activeTenant.tenantId} />
      )}

      {activeConsoleTab === 'providers' && activeTenant && (
        <IdpPanel tenantId={activeTenant.tenantId} />
      )}

      {activeConsoleTab === 'configurations' && activeTenant && (
        <TenantConfigurationsPanel
          tenantId={activeTenant.tenantId}
          tenantName={activeTenant.name}
        />
      )}

      {activeConsoleTab === 'audit' && activeTenant && (
        <TenantAuditPanel tenantId={activeTenant.tenantId} />
      )}
    </DetailPanelShell>
  );
};
