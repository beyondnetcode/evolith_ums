import React from 'react';
import { useI18n } from '@app/i18n/use-i18n';
import { useSystemSuiteDashboard } from '@app/authorization/hooks/use-system-suite-dashboard';
import { SystemSuiteForm } from '../components/SystemSuiteForm';
import { SystemSuiteDetailPanel } from '../components/SystemSuiteDetailPanel';
import { SystemSuiteListPanel } from '../components/SystemSuiteListPanel';
import {
  ConfirmDialog,
  PageDashboardShell,
  AtomicSortOption,
  AtomicFilterOption,
  AtomicQueryCriteriaOption,
} from '@shared/components';

export default function SystemSuiteDashboardScreen(): React.JSX.Element {
  const t = useI18n();
  const dashboard = useSystemSuiteDashboard();

  const criteriaOptions: AtomicQueryCriteriaOption[] = [
    { label: t.byName, value: 'name' },
    { label: t.byCode, value: 'code' },
  ];
  const filterOptions: AtomicFilterOption[] = [
    { label: t.allStatuses, value: 'all' },
    { label: t.active, value: 'Active' },
    { label: t.maintenance, value: 'Maintenance' },
    { label: t.deprecated, value: 'Deprecated' },
  ];
  const sortOptions: AtomicSortOption[] = [
    { label: t.sortByName, value: 'name' },
    { label: t.sortByCode, value: 'code' },
    { label: t.sortByStatus, value: 'status' },
  ];

  const modalOverlays = (
    <>
      <ConfirmDialog
        open={dashboard.showDiscardDialog}
        title={t.unsavedChanges}
        message={t.unsavedChangesMsg}
        cancelLabel={t.cancelEdit}
        confirmLabel={t.discardChanges}
        onConfirm={dashboard.confirmDiscard}
        onCancel={() => dashboard.setShowDiscardDialog(false)}
        variant="danger"
      />
      <SystemSuiteForm
        isOpen={dashboard.isCreateOpen}
        onClose={() => dashboard.setIsCreateOpen(false)}
        onSuccess={dashboard.handleCreateSuccess}
      />
    </>
  );

  return (
    <PageDashboardShell
      splitterLabel="Resize system suite detail panel"
      overlay={modalOverlays}
      master={
        <SystemSuiteListPanel
          systemSuites={dashboard.knownSystemSuites}
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
          onSelectSystemSuite={dashboard.handleSelectSystemSuite}
          criteriaOptions={criteriaOptions}
          filterOptions={filterOptions}
          sortOptions={sortOptions}
          requiresFilter={dashboard.requiresFilter}
        />
      }
      detail={
        <SystemSuiteDetailPanel
          activeSystemSuite={dashboard.activeSystemSuite}
          isLoading={dashboard.isLoadingList}
          onSystemSuiteUpdate={dashboard.patchSystemSuite}
          onEditingChange={dashboard.setIsEditing}
        />
      }
    />
  );
}
