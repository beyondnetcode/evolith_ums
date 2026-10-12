/**
 * ParameterCatalogScreen
 * Main screen for the Parameter Catalog master maintenance
 */
import React from 'react';
import { useI18n } from '@app/i18n/use-i18n';
import { useParameterCatalogDashboard } from '@app/configuration/hooks/use-parameter-catalog-dashboard';
import { ParameterCatalogListPanel } from '../components/ParameterCatalogListPanel';
import { ParameterCatalogDetailPanel } from '../components/ParameterCatalogDetailPanel';
import { ParameterDefinitionForm } from '../components/ParameterDefinitionForm';
import { PageShell } from '@shared/layouts/PageShell';
import { MasterDetailLayout } from '@shared/layouts/MasterDetailLayout';
import { M3Dialog } from '@shared/components/M3Dialog';
import { SortOption, FilterOption, QueryCriteriaOption } from '@shared/components/M3DataView';
import type { CreateParameterDefinitionPayload } from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';

export default function ParameterCatalogScreen(): React.JSX.Element {
  const t = useI18n();
  const d = useParameterCatalogDashboard();

  const criteriaOptions: QueryCriteriaOption[] = [
    { label: t.byCode ?? 'Código', value: 'code' },
    { label: t.byName ?? 'Nombre', value: 'name' },
  ];

  const filterOptions: FilterOption[] = [
    { label: t.allStatuses ?? 'Todos', value: 'all' },
    { label: 'Activo', value: 'true' },
    { label: 'Inactivo', value: 'false' },
  ];

  const sortOptions: SortOption[] = [
    { label: t.sortByCode ?? 'Código', value: 'code' },
    { label: t.sortByName ?? 'Nombre', value: 'name' },
    { label: 'Tipo', value: 'dataTypeId' },
    { label: 'Alcance', value: 'scopeId' },
  ];

  const handleCreate = async (data: CreateParameterDefinitionPayload) => {
    await d.createParameter(data);
  };

  const handleUpdate = async (data: CreateParameterDefinitionPayload) => {
    if (d.selectedId) {
      await d.updateParameter(d.selectedId, data);
    }
  };

  return (
    <PageShell>
      <MasterDetailLayout
        splitterLabel="Resize parameter detail panel"
        overlay={
          <>
            <M3Dialog
              open={d.isDeleteDialogOpen}
              title={t.deleteParameter ?? 'Delete Parameter'}
              message={
                t.deleteParameterConfirm ??
                'Are you sure you want to delete this parameter? This action cannot be undone.'
              }
              onScrimClick={() => d.setIsDeleteDialogOpen(false)}
              actions={[
                {
                  label: t.cancelBtn ?? 'Cancel',
                  variant: 'outlined',
                  onClick: () => d.setIsDeleteDialogOpen(false),
                },
                {
                  label: t.deleteBtn ?? 'Delete',
                  variant: 'filled',
                  className: 'bg-m3-error hover:bg-m3-error/90 border-0',
                  onClick: d.handleDeleteConfirm,
                },
              ]}
            />
            <ParameterDefinitionForm
              isOpen={d.isCreateOpen}
              onClose={() => d.setIsCreateOpen(false)}
              onSubmit={handleCreate}
              isLoading={d.isCreating}
            />
            <ParameterDefinitionForm
              isOpen={d.isEditOpen}
              onClose={() => d.setIsEditOpen(false)}
              onSubmit={handleUpdate}
              editingParameter={d.activeParameter}
              isLoading={d.isUpdating}
            />
          </>
        }
        master={
          <ParameterCatalogListPanel
            parameters={d.knownParameters}
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
            onSelectParameter={d.handleSelect}
            requiresFilter={d.requiresFilter}
          />
        }
        detail={
          <ParameterCatalogDetailPanel
            definitionId={d.selectedId}
            activeParameter={d.activeParameter}
            isLoading={d.isLoadingDetail}
            onEdit={() => d.setIsEditOpen(true)}
            onDelete={() => d.deleteParameter(d.selectedId)}
          />
        }
      />
    </PageShell>
  );
}
