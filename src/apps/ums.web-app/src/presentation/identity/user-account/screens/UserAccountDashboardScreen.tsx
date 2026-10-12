import React from 'react';
import { useI18n } from '@app/i18n/use-i18n';
import { useAuthStore } from '@app/stores/auth.store';
import { useUserAccountDashboard } from '@app/identity/hooks/use-user-account-dashboard';
import { UserAccountForm } from '../components/UserAccountForm';
import { UserAccountDetailPanel } from '../components/UserAccountDetailPanel';
import { UserAccountListPanel } from '../components/UserAccountListPanel';
import { UserSignupsInboxPanel } from '../components/UserSignupsInboxPanel';
import {
  M3FormDialog,
  M3TextField,
  M3Button,
  ConfirmDialog,
  SortOption,
  FilterOption,
  QueryCriteriaOption,
} from '@shared/components';
import { PageShell } from '@shared/layouts/PageShell';
import { MasterDetailLayout } from '@shared/layouts/MasterDetailLayout';
import { Lock } from 'lucide-react';
import { useAccessResolution } from '@app/authorization/hooks/use-access-resolution';

export default function UserAccountDashboardScreen(): React.JSX.Element {
  const t = useI18n();
  const sessionTenantId = useAuthStore(state => state.user?.tenantId);
  const sessionTenantName = useAuthStore(state => state.user?.tenantName);
  const isInternalAdmin = useAuthStore(state => state.user?.isInternalAdmin);
  const dashboard = useUserAccountDashboard(sessionTenantId);
  const { hasOptionAccess } = useAccessResolution();
  const canCreate = hasOptionAccess('USERS_LIST', 'MANAGE_USERS');

  const criteriaOptions: QueryCriteriaOption[] = [
    { label: t.byEmail, value: 'email' },
    { label: t.byUserId, value: 'id' },
  ];
  const filterOptions: FilterOption[] = [
    { label: t.allStatuses, value: 'all' },
    { label: t.active, value: 'Active' },
    { label: t.pending, value: 'Pending' },
    { label: t.blocked, value: 'Blocked' },
  ];
  const sortOptions: SortOption[] = [
    { label: t.sortByEmail, value: 'email' },
    { label: t.sortByStatus, value: 'status' },
    { label: t.sortByCategory, value: 'category' },
  ];

  const modalOverlays = (
    <>
      <M3FormDialog
        open={dashboard.showBlockDialog}
        onClose={() => dashboard.setShowBlockDialog(false)}
        title={t.blockUserTitle}
        icon={<Lock className="w-5 h-5 text-m3-error" />}
        footer={
          <>
            <M3Button
              variant="text"
              onClick={() => dashboard.setShowBlockDialog(false)}
              type="button"
            >
              {t.cancelBtn}
            </M3Button>
            <M3Button
              variant="filled"
              onClick={dashboard.confirmBlock}
              disabled={!dashboard.blockReason.trim()}
              className="bg-m3-error hover:bg-m3-error/90 border-0"
            >
              {t.blockBtn}
            </M3Button>
          </>
        }
      >
        <div className="space-y-3 pt-2">
          <p className="text-[12px] text-m3-secondary">{t.blockUserMessage}</p>
          <M3TextField
            label={t.blockReasonLabel || 'Reason for blocking'}
            required
            value={dashboard.blockReason}
            onChange={e => dashboard.setBlockReason(e.target.value)}
            placeholder="e.g. Security policy violation"
            helperText={
              t.blockReasonHelper || 'Please provide a justification for blocking this account'
            }
          />
        </div>
      </M3FormDialog>

      <ConfirmDialog
        open={dashboard.showRestoreDialog}
        title={t.restoreUserTitle}
        message={t.restoreUserMessage}
        cancelLabel={t.cancelBtn}
        confirmLabel={t.restoreBtn}
        onConfirm={dashboard.confirmRestore}
        onCancel={() => dashboard.setShowRestoreDialog(false)}
      />

      <UserAccountForm
        isOpen={dashboard.isCreateOpen}
        onClose={() => dashboard.setIsCreateOpen(false)}
        onSuccess={dashboard.handleCreateSuccess}
        tenantId={sessionTenantId}
      />
    </>
  );

  return (
    <PageShell>
      {modalOverlays}
      <div className="flex h-full min-h-0 flex-col gap-4">
        {!isInternalAdmin && <UserSignupsInboxPanel />}
        <MasterDetailLayout
          splitterLabel="Resize user account detail panel"
          master={
            <UserAccountListPanel
              accounts={dashboard.knownAccounts}
              selectedId={dashboard.selectedId}
              isLoading={dashboard.isLoadingList}
              error={dashboard.listError}
              viewMode={dashboard.viewMode}
              onViewModeChange={dashboard.setViewMode}
              queryState={dashboard.queryState}
              paginationState={{
                ...dashboard.paginationState,
                totalItems: dashboard.totalItems,
                totalPages: dashboard.totalPages,
              }}
              onRegisterNew={() => dashboard.setIsCreateOpen(true)}
              onAddDisabled={!canCreate}
              onSelectAccount={dashboard.handleSelectAccount}
              criteriaOptions={criteriaOptions}
              filterOptions={filterOptions}
              sortOptions={sortOptions}
              tenants={isInternalAdmin ? dashboard.tenants : undefined}
              selectedTenantId={dashboard.selectedTenantId}
              onTenantChange={isInternalAdmin ? dashboard.setSelectedTenantId : undefined}
              sessionTenantName={sessionTenantName}
              requiresFilter={dashboard.requiresFilter}
              onApproveAccount={dashboard.handleApproveAccount}
            />
          }
          detail={
            <UserAccountDetailPanel
              activeAccount={dashboard.activeAccount}
              isLoading={dashboard.isLoadingList}
              onAccountActivate={dashboard.handleActivate}
              onAccountBlock={dashboard.handleBlockRequest}
              onAccountRestore={dashboard.handleRestoreRequest}
              onAccountUpdate={dashboard.patchAccount}
            />
          }
        />
      </div>
    </PageShell>
  );
}
