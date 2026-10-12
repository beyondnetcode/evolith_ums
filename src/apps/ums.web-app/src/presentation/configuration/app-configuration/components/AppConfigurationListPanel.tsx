import React, { useCallback } from 'react';
import { Settings, Key, Lock, Globe, Building2, Cog, Info } from 'lucide-react';
import type { AppConfiguration } from '@domain/configuration/schemas/app-configuration.schema';
import { useI18n } from '@app/i18n/use-i18n';
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
import { ConfigValueDisplay } from '@presentation/shared/components/ConfigValueDisplay';
import { EntityRow } from '@shared/components/EntityRow';
import { EntityCard } from '@shared/components/EntityCard';
import { ApiErrorBanner } from '@shared/components/ApiErrorBanner';
import type { QueryState } from '@app/shared/hooks/use-query-state';
import type { PaginationState } from '@app/shared/hooks/use-pagination-state';
import { STATUS_COLORS, getStatusLabel } from '@shared/utils/status-utils';

interface AppConfigurationListPanelProps {
  configs: AppConfiguration[];
  selectedId: string;
  isLoading: boolean;
  error: Error | null;
  viewMode: 'list' | 'thumbnail';
  onViewModeChange: (mode: 'list' | 'thumbnail') => void;
  queryState: QueryState;
  paginationState: PaginationState & { totalItems: number; totalPages: number };
  onRegisterNew: () => void;
  onSelectConfig: (id: string) => void;
  requiresFilter: boolean;
  filterOptions?: AtomicFilterOption[];
}

const SCOPE_ICON_MAP: Record<string, React.ReactNode> = {
  Global: <Globe className="w-4 h-4 text-blue-500" />,
  Tenant: <Building2 className="w-4 h-4 text-green-500" />,
  Suite: <Cog className="w-4 h-4 text-purple-500" />,
  Module: <Key className="w-4 h-4 text-orange-500" />,
};

const STATUS_COLOR_MAP = {
  Published: STATUS_COLORS.Published,
  Draft: STATUS_COLORS.Draft,
  Archived: STATUS_COLORS.Archived,
};

export function AppConfigurationListPanel({
  configs,
  selectedId,
  isLoading,
  error,
  viewMode,
  onViewModeChange,
  queryState,
  paginationState,
  onRegisterNew,
  onSelectConfig,
  requiresFilter,
  filterOptions,
}: AppConfigurationListPanelProps): React.JSX.Element {
  const t = useI18n();

  const defaultFilterOptions: AtomicFilterOption[] = [
    { label: 'Todos', value: 'all' },
    { label: 'Borrador', value: 'Draft' },
    { label: 'Publicado', value: 'Published' },
    { label: 'Archivado', value: 'Archived' },
    { label: 'Global', value: 'Global' },
    { label: 'Tenant', value: 'Tenant' },
  ];

  const sortOptions: AtomicSortOption[] = [
    { label: 'Código', value: 'code' },
    { label: 'Scope', value: 'scope' },
    { label: 'Estado', value: 'status' },
  ];

  const criteriaOptions: AtomicQueryCriteriaOption[] = [
    { label: 'By Code', value: 'code' },
    { label: 'By Description', value: 'description' },
  ];

  const renderRow = useCallback(
    (config: AppConfiguration) => {
      const isSelected = config.appConfigurationId === selectedId;
      return (
        <EntityRow
          key={config.appConfigurationId}
          selected={isSelected}
          onClick={() => onSelectConfig(config.appConfigurationId)}
          leading={
            <div
              className={`p-2 rounded-lg shrink-0 transition-colors ${isSelected ? 'bg-m3-primary/15' : 'bg-m3-surface-container/50'}`}
            >
              {SCOPE_ICON_MAP[config.scope] ?? <Settings className="w-4 h-4 text-m3-secondary" />}
            </div>
          }
          trailingColumns={[
            {
              content: <CodeBadge code={config.scope} size="xs" />,
              width: 'w-20',
            },
            {
              content: (
                <StatusBadge
                  status={config.status}
                  label={getStatusLabel(config.status)}
                  colorMap={STATUS_COLOR_MAP}
                />
              ),
              width: 'w-24',
            },
            {
              content: config.isEncrypted ? <Lock className="w-3 h-3 text-amber-500" /> : null,
              width: 'w-8',
            },
          ]}
        >
          <span className="text-[12px] font-medium text-m3-on-surface">{config.code}</span>
          <div className="flex items-center gap-2 mt-0.5 flex-wrap">
            <ConfigValueDisplay value={config.value} truncateAt={40} />
            {config.description && (
              <>
                <span className="text-[9px] text-m3-secondary/30">·</span>
                <span className="text-[10px] text-m3-secondary/70 truncate max-w-[120px]">
                  {config.description}
                </span>
              </>
            )}
          </div>
        </EntityRow>
      );
    },
    [selectedId, onSelectConfig]
  );

  const renderCard = useCallback(
    (config: AppConfiguration) => {
      const isSelected = config.appConfigurationId === selectedId;
      return (
        <EntityCard
          key={config.appConfigurationId}
          selected={isSelected}
          onClick={() => onSelectConfig(config.appConfigurationId)}
          icon={SCOPE_ICON_MAP[config.scope] ?? <Settings className="w-5 h-5" />}
          title={config.code}
          subtitle={
            <span className="text-[10px] text-m3-secondary/60">
              {config.scope} ·{' '}
              {config.description ? config.description.slice(0, 30) : 'Sin descripción'}
            </span>
          }
          badges={
            <div className="flex items-center gap-1">
              <StatusBadge
                status={config.status}
                label={getStatusLabel(config.status)}
                colorMap={STATUS_COLOR_MAP}
              />
              {config.isEncrypted && <Lock className="w-3 h-3 text-amber-500" />}
            </div>
          }
        />
      );
    },
    [selectedId, onSelectConfig]
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
      message="Selecciona un estado o ingresa un término de búsqueda para visualizar las configuraciones."
    />
  ) : null;

  const footerTelemetry = (
    <PaginationFooter
      totalItems={paginationState.totalItems}
      startIndex={paginationState.startIndex ?? 0}
      pageSize={paginationState.pageSize}
      itemLabel="configuraciones"
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
            itemLabel="configuración"
            viewMode={viewMode}
            onViewModeChange={onViewModeChange}
            searchOptions={criteriaOptions}
            activeSearchCriteria={queryState.searchCriteria}
            onSearchCriteriaChange={queryState.setSearchCriteria}
            searchValue={queryState.searchValue}
            onSearchValueChange={queryState.setSearchValue}
            onSearchSubmit={queryState.handleQuerySubmit}
            onSearchClear={queryState.handleResetQuery}
            filterOptions={filterOptions ?? defaultFilterOptions}
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
              emptyLabel="Sin configuraciones"
              emptyTitle="Cree la primera configuración de aplicación."
              viewMode={viewMode}
              renderList={() => (
                <div className="flex flex-col gap-0.5">{configs.map(renderRow)}</div>
              )}
              renderThumbnail={() => (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                  {configs.map(renderCard)}
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
