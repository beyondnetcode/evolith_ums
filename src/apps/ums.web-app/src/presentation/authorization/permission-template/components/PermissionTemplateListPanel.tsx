import React, { useCallback } from 'react';
import { ShieldCheck } from 'lucide-react';
import { type PermissionTemplate } from '@domain/authorization/models/permission-template.model';
import { StatusBadge } from '@shared/components/StatusBadge';
import {
  DataViewShell,
  DataList,
  AtomicFilterOption,
  AtomicSortOption,
  AtomicQueryCriteriaOption,
  PaginationFooter,
  RequiresFilterPrompt,
} from '@shared/components';
import { ListToolbar } from '@shared/components/ListToolbar';
import { useQueryState } from '@app/shared/hooks/use-query-state';
import { usePaginationState } from '@app/shared/hooks/use-pagination-state';
import { EntityRow } from '@shared/components/EntityRow';
import { EntityCard } from '@shared/components/EntityCard';
import { ApiErrorBanner } from '@shared/components/ApiErrorBanner';
import { STATUS_COLORS, getStatusLabel } from '@shared/utils/status-utils';

// ─── Status helpers ───────────────────────────────────────────────────────────

const STATUS_COLOR_MAP = {
  Published: STATUS_COLORS.Published,
  Deprecated: STATUS_COLORS.Deprecated,
  Draft: STATUS_COLORS.Draft,
};

// ─── Props ────────────────────────────────────────────────────────────────────

interface Props {
  templates: PermissionTemplate[];
  selectedId: string;
  isLoading: boolean;
  error: Error | null;
  viewMode: 'list' | 'thumbnail';
  onViewModeChange: (m: 'list' | 'thumbnail') => void;
  queryState: ReturnType<typeof useQueryState<string, string>>;
  paginationState: ReturnType<typeof usePaginationState> & {
    totalItems: number;
    totalPages: number;
  };
  onRegisterNew: () => void;
  onSelectTemplate: (id: string) => void;
  requiresFilter?: boolean;
}

export const PermissionTemplateListPanel: React.FC<Props> = ({
  templates,
  selectedId,
  isLoading,
  error,
  viewMode,
  onViewModeChange,
  queryState,
  paginationState,
  onRegisterNew,
  onSelectTemplate,
  requiresFilter = false,
}) => {
  const totalItems = paginationState.totalItems;
  const criteriaOptions: AtomicQueryCriteriaOption[] = [
    { label: 'Versión', value: 'version' },
    { label: 'Rol', value: 'role' },
    { label: 'Suite', value: 'suite' },
  ];

  const filterOptions: AtomicFilterOption[] = [
    { label: 'Todos', value: 'all' },
    { label: 'Borrador', value: 'Draft' },
    { label: 'Publicada', value: 'Published' },
    { label: 'Descontinuada', value: 'Deprecated' },
  ];

  const sortOptions: AtomicSortOption[] = [
    { label: 'Suite', value: 'suite' },
    { label: 'Rol', value: 'role' },
    { label: 'Versión', value: 'version' },
    { label: 'Estado', value: 'status' },
  ];

  const renderRow = useCallback(
    (tpl: PermissionTemplate) => {
      const isSelected = tpl.templateId === selectedId;
      return (
        <EntityRow
          key={tpl.templateId}
          selected={isSelected}
          onClick={() => onSelectTemplate(tpl.templateId)}
          leading={
            <div
              className={`p-2 rounded-lg shrink-0 transition-colors ${isSelected ? 'bg-m3-primary/15' : 'bg-m3-surface-container/50'}`}
            >
              <ShieldCheck
                className={`w-4 h-4 ${isSelected ? 'text-m3-primary' : 'text-m3-secondary'}`}
              />
            </div>
          }
          trailingColumns={[
            {
              content: (
                <span className="text-[10px] font-mono font-medium text-m3-secondary/60">
                  v{tpl.version}
                </span>
              ),
              width: 'w-14',
            },
            {
              content: (
                <StatusBadge
                  status={tpl.status}
                  label={getStatusLabel(tpl.status)}
                  colorMap={STATUS_COLOR_MAP}
                />
              ),
              width: 'w-24',
            },
          ]}
        >
          <span className="text-sm font-semibold text-m3-on-surface truncate">{tpl.roleName}</span>
          <span className="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-m3-primary/10 text-m3-primary font-mono mt-0.5 self-start">
            {tpl.systemSuiteName}
          </span>
        </EntityRow>
      );
    },
    [selectedId, onSelectTemplate]
  );

  const renderCard = useCallback(
    (tpl: PermissionTemplate) => {
      const isSelected = tpl.templateId === selectedId;
      return (
        <EntityCard
          key={tpl.templateId}
          selected={isSelected}
          onClick={() => onSelectTemplate(tpl.templateId)}
          icon={<ShieldCheck className="w-5 h-5" />}
          title={tpl.roleName}
          subtitle={
            <div className="flex items-center gap-1.5 mt-0.5 flex-wrap">
              <span className="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-m3-primary/10 text-m3-primary font-mono">
                {tpl.systemSuiteName}
              </span>
              <span className="text-[10px] font-mono text-m3-secondary/60">v{tpl.version}</span>
            </div>
          }
          badges={
            <StatusBadge
              status={tpl.status}
              label={getStatusLabel(tpl.status)}
              colorMap={STATUS_COLOR_MAP}
            />
          }
        />
      );
    },
    [selectedId, onSelectTemplate]
  );

  const pagination =
    paginationState.totalPages > 0
      ? {
          page: paginationState.page,
          pageSize: paginationState.pageSize,
          totalItems: paginationState.totalItems,
          totalPages: paginationState.totalPages,
          onPageChange: paginationState.handlePageChange ?? paginationState.setPage,
          onPageSizeChange: paginationState.handlePageSizeChange,
        }
      : undefined;

  const filterPrompt = requiresFilter ? (
    <RequiresFilterPrompt
      title="Aplica un filtro para cargar"
      message="Selecciona un estado o ingresa un término de búsqueda para visualizar las plantillas."
    />
  ) : null;

  const footerTelemetry = (
    <PaginationFooter
      totalItems={paginationState.totalItems}
      startIndex={paginationState.startIndex ?? 0}
      pageSize={paginationState.pageSize}
      itemLabel="plantillas"
      onClear={queryState.handleResetQuery}
      searchTerm={queryState.appliedQuery.term}
    />
  );

  return (
    <div className="h-full flex flex-col">
      {error && <ApiErrorBanner error={error} className="mb-2" />}
      <DataViewShell
        title="Plantillas"
        subtitle="Mantenimiento de plantillas de permisos."
        controls={
          <ListToolbar
            itemCount={totalItems}
            itemLabel="plantilla"
            viewMode={viewMode}
            onViewModeChange={onViewModeChange}
            searchOptions={criteriaOptions}
            activeSearchCriteria={queryState.searchCriteria}
            onSearchCriteriaChange={queryState.setSearchCriteria}
            searchValue={queryState.searchValue}
            onSearchValueChange={queryState.setSearchValue}
            onSearchSubmit={queryState.handleQuerySubmit}
            onSearchClear={queryState.handleResetQuery}
            filterOptions={filterOptions}
            activeFilter={queryState.activeFilter}
            onFilterChange={queryState.setActiveFilter}
            sortOptions={sortOptions}
            sortBy={queryState.sortBy}
            onSortByChange={queryState.setSortBy}
            sortOrder={queryState.sortOrder}
            onSortOrderToggle={queryState.toggleSortOrder}
            onAdd={onRegisterNew}
          />
        }
        content={
          requiresFilter && !queryState.appliedQuery.filterApplied ? (
            filterPrompt
          ) : (
            <DataList
              isLoading={isLoading}
              isEmpty={totalItems === 0}
              emptyTitle="Sin plantillas de permisos"
              emptyLabel="Cree la primera plantilla para este contexto de autorización."
              viewMode={viewMode}
              renderList={() => (
                <div className="flex flex-col gap-0.5">{templates.map(renderRow)}</div>
              )}
              renderThumbnail={() => (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                  {templates.map(renderCard)}
                </div>
              )}
              pagination={pagination}
              footerElement={footerTelemetry}
            />
          )
        }
      />
    </div>
  );
};
