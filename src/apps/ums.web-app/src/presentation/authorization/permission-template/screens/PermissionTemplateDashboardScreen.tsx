/**
 * PermissionTemplateDashboardScreen
 *
 * Master-detail layout for the Permission Template aggregate:
 *  • Left panel  — paginated list with filter/sort controls
 *  • Right panel — detail view (overview + items) for the selected template
 *  • Modal       — create new template form
 */
import React from 'react';
import { usePermissionTemplateDashboard } from '@app/authorization/hooks/use-permission-template-dashboard';
import { PermissionTemplateListPanel } from '../components/PermissionTemplateListPanel';
import { PermissionTemplateDetailPanel } from '../components/PermissionTemplateDetailPanel';
import { PermissionTemplateForm } from '../components/PermissionTemplateForm';
import { PageShell } from '@shared/layouts/PageShell';
import { MasterDetailLayout } from '@shared/layouts/MasterDetailLayout';

export default function PermissionTemplateDashboardScreen(): React.JSX.Element {
  const d = usePermissionTemplateDashboard();

  return (
    <PageShell>
      <PermissionTemplateForm
        isOpen={d.isCreateOpen}
        onClose={() => d.setIsCreateOpen(false)}
        onSuccess={templateId => {
          d.handleCreateSuccess();
          d.setSelectedId(templateId);
        }}
      />

      <MasterDetailLayout
        splitterLabel="Resize permission template detail panel"
        master={
          <PermissionTemplateListPanel
            templates={d.knownTemplates}
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
            onSelectTemplate={d.handleSelect}
            requiresFilter={d.requiresFilter}
          />
        }
        detail={
          <PermissionTemplateDetailPanel
            template={d.activeTemplate}
            isLoading={d.isLoadingDetail}
            onDeleted={() => d.setSelectedId('')}
          />
        }
      />
    </PageShell>
  );
}
