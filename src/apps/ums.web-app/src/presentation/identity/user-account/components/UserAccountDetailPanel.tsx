import React, { useState } from 'react';
import { User, Shield, Key } from 'lucide-react';
import { UserAccount } from '@domain/identity/models/user-account.model';
import { UserAccountProfileCard } from './UserAccountProfileCard';
import { UserAccountPasswordPanel } from './UserAccountPasswordPanel';
import { UserAccountAccessView } from './UserAccountAccessView';
import { DetailPanelShell, EmptyDetailState } from '@shared/components';
import type { DetailTab } from '@shared/components/DetailPanelShell';
import { useI18n } from '@app/i18n/use-i18n';

type UserAccountTab = 'overview' | 'accesos' | 'credentials';

interface UserAccountDetailPanelProps {
  activeAccount: UserAccount | undefined;
  isLoading: boolean;
  onAccountActivate: () => void;
  onAccountBlock: (userAccountId: string) => void;
  onAccountRestore: (userAccountId: string) => void;
  onAccountUpdate: (accountId: string, patch: Partial<UserAccount>) => void;
}

export const UserAccountDetailPanel: React.FC<UserAccountDetailPanelProps> = ({
  activeAccount,
  isLoading,
  onAccountActivate,
  onAccountBlock,
  onAccountRestore,
  onAccountUpdate,
}) => {
  const t = useI18n();
  const [activeTab, setActiveTab] = useState<UserAccountTab>('overview');

  const tabs: DetailTab<UserAccountTab>[] = [
    { key: 'overview', label: t.overview, icon: <User className="w-3.5 h-3.5" /> },
    { key: 'accesos', label: 'Accesos', icon: <Shield className="w-3.5 h-3.5" /> },
    { key: 'credentials', label: t.credentials, icon: <Key className="w-3.5 h-3.5" /> },
  ];

  if (isLoading) {
    return (
      <DetailPanelShell
        isLoading={true}
        isEmpty={false}
        loadingLabel={t.loadingProfile ?? 'Loading...'}
        tabs={tabs}
        activeTab={activeTab}
        onTabChange={setActiveTab}
      >
        <div />
      </DetailPanelShell>
    );
  }

  if (!activeAccount) {
    return (
      <DetailPanelShell
        isLoading={false}
        isEmpty={true}
        emptyLabel=""
        tabs={tabs}
        activeTab={activeTab}
        onTabChange={setActiveTab}
      >
        <EmptyDetailState
          icon={User}
          title={t.selectAccountToView ?? 'Select an account'}
          description="Choose an account from the list to view its details."
        />
      </DetailPanelShell>
    );
  }

  return (
    <DetailPanelShell
      isLoading={false}
      isEmpty={false}
      tabs={tabs}
      activeTab={activeTab}
      onTabChange={setActiveTab}
      entityKey={activeAccount.userAccountId}
      header={
        <UserAccountProfileCard
          account={activeAccount}
          onActivate={() => onAccountActivate()}
          onBlock={() => onAccountBlock(activeAccount.userAccountId)}
          onRestore={() => onAccountRestore(activeAccount.userAccountId)}
          onAccountUpdate={onAccountUpdate}
        />
      }
    >
      <div className="space-y-4">
        {activeTab === 'overview' && (
          <div>
            <h3 className="text-sm font-medium text-m3-on-surface mb-2">{t.accountDetails}</h3>
            <dl className="space-y-2 text-sm">
              <div className="flex justify-between">
                <dt className="text-m3-secondary">{t.userEmail}</dt>
                <dd className="text-m3-on-surface font-mono">{activeAccount.email}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-m3-secondary">{t.userCategory}</dt>
                <dd className="text-m3-on-surface">{activeAccount.category}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-m3-secondary">{t.status}</dt>
                <dd className="text-m3-on-surface">{activeAccount.status}</dd>
              </div>
            </dl>
          </div>
        )}

        {activeTab === 'accesos' && (
          <UserAccountAccessView
            userId={activeAccount.userAccountId}
            tenantId={activeAccount.tenantId}
          />
        )}

        {activeTab === 'credentials' && <UserAccountPasswordPanel account={activeAccount} />}
      </div>
    </DetailPanelShell>
  );
};
