import React, { useCallback } from 'react';
import { Tag, Info } from 'lucide-react';
import type { ParameterDefinition } from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';
import { useI18n } from '@app/i18n/use-i18n';
import {
  DataTypeLabels,
  ScopeLabels,
} from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';
import {
  DataViewShell,
  DataList,
  AtomicSortOption,
  AtomicFilterOption,
  AtomicQueryCriteriaOption,
  PaginationFooter,
  RequiresFilterPrompt,
} from '@shared/components';
import { ListToolbar } from '@shared/components/ListToolbar';
import { StatusBadge } from '@shared/components/StatusBadge';
import { CodeBadge } from '@shared/components/CodeBadge';
import { EntityRow } from '@shared/components/EntityRow';
import { EntityCard } from '@shared/components/EntityCard';
import { ApiErrorBanner } from '@shared/components/ApiErrorBanner';
import type { QueryState } from '@app/shared/hooks/use-query-state';
import type { PaginationState } from '@app/shared/hooks/use-pagination-state';
import { STATUS_COLORS } from '@shared/utils/status-utils';

interface ParameterCatalogListPanelProps {
  parameters: ParameterDefinition[];
  selectedId: string;
  isLoading: boolean;
  error: Error | null;
  viewMode: 'list' | 'thumbnail';
  onViewModeChange: (mode: 'list' | 'thumbnail') => void;
  queryState: QueryState;
  paginationState: PaginationState & { totalItems: number; totalPages: number };
  onRegisterNew: () => void;
  onSelectParameter: (id: string) => void;
  requiresFilter: boolean;
}

const STATUS_COLOR_MAP = {
  Active: STATUS_COLORS.Active,
  Inactive: STATUS_COLORS.Inactive,
};

const SCOPE_COLOR: Record<number, string> = {
  1: 'bg-blue-500/10 text-blue-500',
  2: 'bg-green-500/10 text-green-500',
  3: 'bg-purple-500/10 text-purple-500',
};

const TYPE_COLOR: Record<number, string> = {
  1: 'bg-slate-500/10 text-slate-600',
  2: 'bg-orange-500/10 text-orange-600',
  3: 'bg-cyan-500/10 text-cyan-600',
  4: 'bg-rose-500/10 text-rose-600',
};

export function ParameterCatalogListPanel({
  parameters,
  selectedId,
  isLoading,
  error,
  viewMode,
  onViewModeChange,
  queryState,
  paginationState,
  onRegisterNew,
  onSelectParameter,
  requiresFilter,
}: ParameterCatalogListPanelProps): React.JSX.Element {
  const t = useI18n();

  const filterOptions: AtomicFilterOption[] = [
    { label: 'Todos', value: 'all' },
    { label: 'Activo', value: 'true' },
    { label: 'Inactivo', value: 'false' },
  ];

  const sortOptions: AtomicSortOption[] = [
    { label: 'Código', value: 'code' },
    { label: 'Nombre', value: 'name' },
    { label: 'Tipo', value: 'dataTypeId' },
    { label: 'Alcance', value: 'scopeId' },
  ];

  const criteriaOptions: AtomicQueryCriteriaOption[] = [
    { label: 'By Code', value: 'code' },
    { label: 'By Name', value: 'name' },
    { label: 'By Description', value: 'description' },
  ];

  const renderRow = useCallback(
    (param: ParameterDefinition) => {
      const isSelected = param.id === selectedId;
      return (
        <EntityRow
          key={param.id}
          selected={isSelected}
          onClick={() => onSelectParameter(param.id)}
          leading={
            <div
              className={`p-2 rounded-lg shrink-0 transition-colors ${isSelected ? 'bg-m3-primary/15' : 'bg-m3-surface-container/50'}`}
            >
              <Tag className={`w-4 h-4 ${isSelected ? 'text-m3-primary' : 'text-m3-secondary'}`} />
            </div>
          }
          trailingColumns={[
            {
              content: (
                <span
                  className={`text-[10px] font-bold uppercase px-1.5 py-0.5 rounded ${SCOPE_COLOR[param.scopeId] ?? 'bg-m3-surface-variant'}`}
                >
                  {ScopeLabels[param.scopeId] ?? 'Scope'}
                </span>
              ),
              width: 'w-20',
            },
            {
              content: (
                <span
                  className={`text-[10px] font-bold uppercase px-1.5 py-0.5 rounded ${TYPE_COLOR[param.dataTypeId] ?? 'bg-m3-surface-variant'}`}
                >
                  {DataTypeLabels[param.dataTypeId] ?? 'Type'}
                </span>
              ),
              width: 'w-20',
            },
            {
              content: (
                <StatusBadge
                  status={param.isActive ? 'Active' : 'Inactive'}
                  label={param.isActive ? 'Activo' : 'Inactivo'}
                  colorMap={STATUS_COLOR_MAP}
                />
              ),
              width: 'w-20',
            },
            {
              content: param.isMandatory ? (
                <span className="text-[10px] text-orange-500 font-bold">*</span>
              ) : null,
              width: 'w-8',
            },
          ]}
        >
          <span className="text-[12px] font-medium text-m3-on-surface">{param.name}</span>
          <div className="flex items-center gap-2 mt-0.5 flex-wrap">
            <CodeBadge code={param.code} size="xs" />
            {param.description && (
              <>
                <span className="text-[9px] text-m3-secondary/30">·</span>
                <span className="text-[10px] text-m3-secondary/70 truncate max-w-[120px]">
                  {param.description}
                </span>
              </>
            )}
          </div>
        </EntityRow>
      );
    },
    [selectedId, onSelectParameter]
  );

  const renderCard = useCallback(
    (param: ParameterDefinition) => {
      const isSelected = param.id === selectedId;
      return (
        <EntityCard
          key={param.id}
          selected={isSelected}
          onClick={() => onSelectParameter(param.id)}
          icon={<Tag className="w-5 h-5" />}
          title={param.name}
          subtitle={
            <span className="text-[10px] text-m3-secondary/60">
              {param.code} · {ScopeLabels[param.scopeId]} · {DataTypeLabels[param.dataTypeId]}
            </span>
          }
          badges={
            <div className="flex items-center gap-1">
              <StatusBadge
                status={param.isActive ? 'Active' : 'Inactive'}
                label={param.isActive ? 'Activo' : 'Inactivo'}
                colorMap={STATUS_COLOR_MAP}
              />
              {param.isMandatory && (
                <span className="text-[10px] text-orange-500 font-bold">*</span>
              )}
            </div>
          }
        />
      );
    },
    [selectedId, onSelectParameter]
  );

  const totalItems = paginationState.totalItems;
  const startIndex = paginationState.startIndex ?? 0;
  const pageSize = paginationState.pageSize;

  const pagination =
    paginationState.totalPages > 0
      ? {
          page: paginationState.page,
          pageSize: paginationState.pageSize,
          totalItems: paginationState.totalItems,
          totalPages: paginationState.totalPages,
          onPageChange: paginationState.setPage,
          onPageSizeChange: paginationState.setPageSize,
        }
      : undefined;

  const filterPrompt = requiresFilter ? (
    <RequiresFilterPrompt
      title="Aplica un filtro para cargar"
      message="Selecciona un estado o ingresa un término de búsqueda para visualizar los parámetros."
    />
  ) : null;

  const footerTelemetry = (
    <PaginationFooter
      totalItems={paginationState.totalItems}
      startIndex={paginationState.startIndex ?? 0}
      pageSize={paginationState.pageSize}
      itemLabel="parámetros"
      onClear={queryState.handleResetQuery}
      searchTerm={queryState.appliedQuery.term}
    />
  );

  return (
    <div className="h-full flex flex-col">
      {error && <ApiErrorBanner error={error} className="mb-2" />}
      <DataViewShell
        controls={
          <ListToolbar
            itemCount={totalItems}
            itemLabel="parámetro"
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
              emptyLabel="Sin parámetros"
              emptyTitle="Cree el primer parámetro del catálogo."
              viewMode={viewMode}
              renderList={() => (
                <div className="flex flex-col gap-0.5">{parameters.map(renderRow)}</div>
              )}
              renderThumbnail={() => (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                  {parameters.map(renderCard)}
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
}
