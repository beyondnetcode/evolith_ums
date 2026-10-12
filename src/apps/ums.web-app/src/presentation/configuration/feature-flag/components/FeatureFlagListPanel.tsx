import React, { useCallback } from 'react';
import { Flag, Info } from 'lucide-react';
import { type FeatureFlag } from '@domain/configuration/models/feature-flag.model';
import { StatusBadge } from '@shared/components/StatusBadge';
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
import { useQueryState } from '@app/shared/hooks/use-query-state';
import { usePaginationState } from '@app/shared/hooks/use-pagination-state';
import { EntityRow } from '@shared/components/EntityRow';
import { EntityCard } from '@shared/components/EntityCard';
import { ApiErrorBanner } from '@shared/components/ApiErrorBanner';
import { CodeBadge } from '@shared/components/CodeBadge';
import { FLAG_TYPE_LABELS } from '@domain/configuration/constants/feature-flag.constants';
import { STATUS_COLORS, getStatusLabel } from '@shared/utils/status-utils';

const STATUS_COLOR_MAP = {
  Active: STATUS_COLORS.Active,
  Inactive: STATUS_COLORS.Inactive,
  Archived: STATUS_COLORS.Archived,
};

const TYPE_COLOR: Record<string, string> = {
  Boolean: 'bg-blue-500/10 text-blue-500',
  Variant: 'bg-purple-500/10 text-purple-500',
  Percentage: 'bg-emerald-500/10 text-emerald-500',
};

function getSystemSuiteLabel(flag: FeatureFlag): string {
  const suiteName = flag.systemSuiteName?.trim();
  const suiteCode = flag.systemSuiteCode?.trim();

  if (suiteName && suiteCode) return `${suiteName} (${suiteCode})`;
  if (suiteName) return suiteName;
  if (suiteCode) return suiteCode;
  return 'Sistema desconocido';
}

interface Props {
  flags: FeatureFlag[];
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
  onSelectFlag: (id: string) => void;
  requiresFilter?: boolean;
}

export const FeatureFlagListPanel: React.FC<Props> = ({
  flags,
  selectedId,
  isLoading,
  error,
  viewMode,
  onViewModeChange,
  queryState,
  paginationState,
  onRegisterNew,
  onSelectFlag,
  requiresFilter = false,
}) => {
  const filterOptions: AtomicFilterOption[] = [
    { label: 'Todos', value: 'all' },
    { label: 'Activo', value: 'Active' },
    { label: 'Inactivo', value: 'Inactive' },
    { label: 'Archivado', value: 'Archived' },
  ];

  const sortOptions: AtomicSortOption[] = [
    { label: 'Código', value: 'flagCode' },
    { label: 'Tipo', value: 'flagType' },
    { label: 'Estado', value: 'status' },
  ];

  const criteriaOptions: AtomicQueryCriteriaOption[] = [{ label: 'By Code', value: 'flagCode' }];

  const renderRow = useCallback(
    (flag: FeatureFlag) => {
      const isSelected = flag.featureFlagId === selectedId;
      return (
        <EntityRow
          key={flag.featureFlagId}
          selected={isSelected}
          onClick={() => onSelectFlag(flag.featureFlagId)}
          leading={
            <div
              className={`p-2 rounded-lg shrink-0 transition-colors ${isSelected ? 'bg-m3-primary/15' : 'bg-m3-surface-container/50'}`}
            >
              <Flag className={`w-4 h-4 ${isSelected ? 'text-m3-primary' : 'text-m3-secondary'}`} />
            </div>
          }
          trailingColumns={[
            {
              content: (
                <span
                  className={`text-[10px] font-bold uppercase px-1.5 py-0.5 rounded ${TYPE_COLOR[flag.flagType] ?? 'bg-m3-surface-variant'}`}
                >
                  {FLAG_TYPE_LABELS[flag.flagType] ?? flag.flagType}
                </span>
              ),
              width: 'w-24',
            },
            {
              content: (
                <StatusBadge
                  status={flag.status}
                  label={getStatusLabel(flag.status)}
                  colorMap={STATUS_COLOR_MAP}
                />
              ),
              width: 'w-24',
            },
          ]}
        >
          <span className="text-[12px] font-medium text-m3-on-surface">{flag.flagCode}</span>
          <div className="flex items-center gap-2 mt-0.5 flex-wrap">
            <span className="inline-flex items-center gap-1 text-[10px] text-m3-secondary/70">
              <span className="text-[9px] font-semibold uppercase tracking-wide text-m3-secondary/50">
                Suite
              </span>
              <span>{getSystemSuiteLabel(flag)}</span>
            </span>
            {flag.rolloutPercentage != null && (
              <>
                <span className="text-[9px] text-m3-secondary/30">·</span>
                <span className="text-[10px] text-m3-secondary/70">
                  Rollout: {flag.rolloutPercentage}%
                </span>
              </>
            )}
            {flag.criteria.length > 0 && (
              <>
                <span className="text-[9px] text-m3-secondary/30">·</span>
                <span className="text-[10px] text-m3-secondary/70">
                  {flag.criteria.length} criterio{flag.criteria.length > 1 ? 's' : ''}
                </span>
              </>
            )}
          </div>
        </EntityRow>
      );
    },
    [selectedId, onSelectFlag]
  );

  const renderCard = useCallback(
    (flag: FeatureFlag) => {
      const isSelected = flag.featureFlagId === selectedId;
      return (
        <EntityCard
          key={flag.featureFlagId}
          selected={isSelected}
          onClick={() => onSelectFlag(flag.featureFlagId)}
          icon={<Flag className="w-5 h-5" />}
          title={flag.flagCode}
          subtitle={
            <span className="text-[10px] text-m3-secondary/60">
              {FLAG_TYPE_LABELS[flag.flagType]} · Suite {getSystemSuiteLabel(flag)}
            </span>
          }
          badges={
            <div className="flex items-center gap-1">
              {flag.rolloutPercentage != null && (
                <CodeBadge code={`${flag.rolloutPercentage}%`} size="xs" />
              )}
              <StatusBadge
                status={flag.status}
                label={getStatusLabel(flag.status)}
                colorMap={STATUS_COLOR_MAP}
              />
            </div>
          }
        />
      );
    },
    [selectedId, onSelectFlag]
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
          onPageChange: paginationState.handlePageChange ?? paginationState.setPage,
          onPageSizeChange: paginationState.handlePageSizeChange,
        }
      : undefined;

  const filterPrompt = requiresFilter ? (
    <RequiresFilterPrompt
      title="Aplica un filtro para cargar"
      message="Selecciona un estado o ingresa un término de búsqueda para visualizar los feature flags."
    />
  ) : null;

  const footerTelemetry = (
    <PaginationFooter
      totalItems={paginationState.totalItems}
      startIndex={paginationState.startIndex ?? 0}
      pageSize={paginationState.pageSize}
      itemLabel="feature flags"
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
            itemLabel="feature flag"
            itemLabelPlural="feature flags"
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
              emptyLabel="Sin feature flags"
              emptyTitle="Cree el primer feature flag para controlar funcionalidades."
              viewMode={viewMode}
              renderList={() => <div className="flex flex-col gap-0.5">{flags.map(renderRow)}</div>}
              renderThumbnail={() => (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                  {flags.map(renderCard)}
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
