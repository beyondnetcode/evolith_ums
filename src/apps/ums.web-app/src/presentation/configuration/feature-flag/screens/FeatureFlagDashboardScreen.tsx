/**
 * FeatureFlagDashboardScreen
 *
 * Master-detail layout for the FeatureFlag aggregate:
 *  • Left panel  — paginated list with filter/sort controls
 *  • Right panel — detail view (overview + criteria) for the selected flag
 *  • Modal       — create new feature flag form
 */
import React from 'react';
import { useFeatureFlagDashboard } from '@app/configuration/hooks/use-feature-flag-dashboard';
import { FeatureFlagListPanel } from '../components/FeatureFlagListPanel';
import { FeatureFlagDetailPanel } from '../components/FeatureFlagDetailPanel';
import { FeatureFlagForm } from '../components/FeatureFlagForm';
import { PageShell } from '@shared/layouts/PageShell';
import { MasterDetailLayout } from '@shared/layouts/MasterDetailLayout';

export default function FeatureFlagDashboardScreen(): React.JSX.Element {
  const d = useFeatureFlagDashboard();

  return (
    <PageShell>
      <FeatureFlagForm
        isOpen={d.isCreateOpen}
        onClose={() => d.setIsCreateOpen(false)}
        onSuccess={featureFlagId => {
          d.handleCreateSuccess();
          d.setSelectedId(featureFlagId);
        }}
      />

      <MasterDetailLayout
        splitterLabel="Resize feature flag detail panel"
        master={
          <FeatureFlagListPanel
            flags={d.knownFlags}
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
            onRegisterNew={() => d.setIsCreateOpen(true)}
            onSelectFlag={d.handleSelect}
            requiresFilter={d.requiresFilter}
          />
        }
        detail={<FeatureFlagDetailPanel flag={d.activeFlag} />}
      />
    </PageShell>
  );
}
