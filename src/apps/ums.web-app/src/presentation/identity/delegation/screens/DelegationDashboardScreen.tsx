import React from 'react';
import { useI18n } from '@app/i18n/use-i18n';
import { useDelegationDashboard } from '@app/identity/hooks/use-delegation-dashboard';
import { DelegationForm } from '../components/DelegationForm';
import { DelegationDetailPanel } from '../components/DelegationDetailPanel';
import { DelegationListPanel } from '../components/DelegationListPanel';
import { PageShell } from '@shared/layouts/PageShell';
import { MasterDetailLayout } from '@shared/layouts/MasterDetailLayout';
import { M3Dialog } from '@shared/components/M3Dialog';
import {
  AtomicSortOption,
  AtomicFilterOption,
  AtomicQueryCriteriaOption,
} from '@shared/components';

export default function DelegationDashboardScreen(): React.JSX.Element {
  const t = useI18n();
  const dashboard = useDelegationDashboard();

  const criteriaOptions: AtomicQueryCriteriaOption[] = [
    { label: 'By ID', value: 'id' },
    { label: 'By Scope', value: 'scope' },
  ];
  const filterOptions: AtomicFilterOption[] = [
    { label: t.allStatuses ?? 'All Statuses', value: 'all' },
    { label: t.active ?? 'Active', value: 'Active' },
    { label: t.suspended ?? 'Suspended', value: 'Suspended' },
    { label: 'Pending Approval', value: 'PendingApproval' },
    { label: 'Revoked', value: 'Revoked' },
  ];
  const sortOptions: AtomicSortOption[] = [
    { label: t.sortByStatus ?? 'Status', value: 'status' },
    { label: 'Valid From', value: 'validFrom' },
  ];

  return (
    <PageShell>
      <MasterDetailLayout
        splitterLabel="Resize delegation detail panel"
        overlay={
          <>
            <M3Dialog
              open={dashboard.showDiscardDialog}
              title={t.unsavedChanges ?? 'Unsaved Changes'}
              message={
                t.unsavedChangesMsg ??
                'You have unsaved changes. Are you sure you want to discard them?'
              }
              onScrimClick={() => dashboard.setShowDiscardDialog(false)}
              actions={[
                {
                  label: t.cancelEdit ?? 'Cancel',
                  variant: 'outlined',
                  onClick: () => dashboard.setShowDiscardDialog(false),
                },
                {
                  label: t.discardChanges ?? 'Discard',
                  variant: 'filled',
                  className: 'bg-m3-error hover:bg-m3-error/90 border-0',
                  onClick: dashboard.confirmDiscard,
                },
              ]}
            />
            <DelegationForm
              isOpen={dashboard.isCreateOpen}
              onClose={() => dashboard.setIsCreateOpen(false)}
              onSuccess={dashboard.handleCreateSuccess}
              defaultTenantId={dashboard.currentTenantId}
              defaultDelegatingAdminId={dashboard.currentUserId}
            />
          </>
        }
        master={
          <DelegationListPanel
            delegations={dashboard.knownDelegations}
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
            onSelectDelegation={dashboard.handleSelectDelegation}
            criteriaOptions={criteriaOptions}
            filterOptions={filterOptions}
            sortOptions={sortOptions}
            delegationViewType={dashboard.delegationViewType}
            onDelegationViewTypeChange={dashboard.setDelegationViewType}
            requiresFilter={dashboard.requiresFilter}
          />
        }
        detail={
          <DelegationDetailPanel
            selectedId={dashboard.selectedId}
            activeDelegation={dashboard.activeDelegation}
            isLoading={dashboard.isLoadingList}
            activeConsoleTab={dashboard.activeConsoleTab}
            consoleTabs={dashboard.consoleTabs}
            onConsoleTabChange={dashboard.setActiveConsoleTab}
          />
        }
      />
    </PageShell>
  );
}
