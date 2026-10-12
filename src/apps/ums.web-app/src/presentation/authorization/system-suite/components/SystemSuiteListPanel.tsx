import React, { useCallback } from 'react';
import { Box, ArrowRight, Info } from 'lucide-react';
import { SystemSuite } from '@domain/authorization/models/system-suite.model';
import { StatusBadge } from '@shared/components/StatusBadge';
import { CodeBadge } from '@shared/components/CodeBadge';
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
import { useI18n } from '@app/i18n/use-i18n';
import { useStatusLabel } from '@app/hooks/use-status-label';
import { ApiErrorBanner } from '@shared/components/ApiErrorBanner';

interface SystemSuiteListPanelProps {
  systemSuites: SystemSuite[];
  selectedId: string;
  isLoading: boolean;
  error: Error | null;
  viewMode: 'list' | 'thumbnail';
  onViewModeChange: (mode: 'list' | 'thumbnail') => void;
  queryState: ReturnType<typeof useQueryState<string, string>>;
  paginationState: ReturnType<typeof usePaginationState> & {
    totalItems: number;
    totalPages: number;
  };
  onRegisterNew: () => void;
  onSelectSystemSuite: (systemSuiteId: string) => void;
  criteriaOptions: AtomicQueryCriteriaOption[];
  filterOptions: AtomicFilterOption[];
  sortOptions: AtomicSortOption[];
  requiresFilter?: boolean;
}

export const SystemSuiteListPanel: React.FC<SystemSuiteListPanelProps> = ({
  systemSuites,
  selectedId,
  isLoading,
  error,
  viewMode,
  onViewModeChange,
  queryState,
  paginationState,
  onRegisterNew,
  onSelectSystemSuite,
  criteriaOptions,
  filterOptions,
  sortOptions,
  requiresFilter = false,
}) => {
  const t = useI18n();
  const getStatusLabel = useStatusLabel();

  const renderSystemSuiteRow = useCallback(
    (systemSuite: SystemSuite) => {
      const isSelected = systemSuite.systemSuiteId === selectedId;
      return (
        <EntityRow
          key={systemSuite.systemSuiteId}
          id={systemSuite.systemSuiteId}
          isActive={systemSuite.status === 'Active'}
          selected={isSelected}
          onClick={() => onSelectSystemSuite(systemSuite.systemSuiteId)}
          leading={
            <div
              className={`p-2 rounded-lg transition-colors ${isSelected ? 'bg-m3-primary/15' : 'bg-m3-surface-container/50'}`}
            >
              <Box className={`w-4 h-4 ${isSelected ? 'text-m3-primary' : 'text-m3-secondary'}`} />
            </div>
          }
          trailingColumns={[
            { content: <CodeBadge code={systemSuite.code} />, width: 'w-20' },
            {
              content: (
                <StatusBadge
                  status={systemSuite.status}
                  label={getStatusLabel(systemSuite.status)}
                />
              ),
              width: 'w-20',
            },
            {
              content: (
                <ArrowRight
                  className={`w-4 h-4 transition-transform ${isSelected ? 'text-m3-primary translate-x-0.5' : 'text-m3-outline/30'}`}
                />
              ),
              width: 'w-5',
            },
          ]}
        >
          <span className="text-sm font-semibold text-m3-on-surface line-clamp-1">
            {systemSuite.name}
          </span>
        </EntityRow>
      );
    },
    [selectedId, onSelectSystemSuite, getStatusLabel]
  );

  const renderSystemSuiteCard = useCallback(
    (systemSuite: SystemSuite) => {
      const isSelected = systemSuite.systemSuiteId === selectedId;
      return (
        <EntityCard
          key={systemSuite.systemSuiteId}
          selected={isSelected}
          onClick={() => onSelectSystemSuite(systemSuite.systemSuiteId)}
          icon={<Box className="w-5 h-5" />}
          title={systemSuite.name}
          subtitle={systemSuite.code}
          badges={
            <>
              <CodeBadge code={systemSuite.code} />
              <StatusBadge status={systemSuite.status} label={getStatusLabel(systemSuite.status)} />
            </>
          }
        />
      );
    },
    [selectedId, onSelectSystemSuite, getStatusLabel]
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
    <RequiresFilterPrompt title={t.applyFilterTitle} message={t.applyFilterMessage} />
  ) : null;

  const footerTelemetry = (
    <PaginationFooter
      totalItems={paginationState.totalItems}
      startIndex={paginationState.startIndex ?? 0}
      pageSize={paginationState.pageSize}
      itemLabel={t.systemSuites ?? 'suites'}
      onClear={queryState.handleResetQuery}
      searchTerm={queryState.appliedQuery.term}
    />
  );

  return (
    <DataViewShell
      title={t.systemSuiteMaintenance}
      subtitle={t.systemSuiteMaintenanceSubtitle}
      controls={
        <ListToolbar
          itemCount={totalItems}
          itemLabel="suite"
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
            isEmpty={!isLoading && systemSuites.length === 0}
            emptyLabel={
              queryState.appliedQuery.term
                ? (t.noResultsFound ?? 'No results found')
                : (t.noSystemSuitesFound ?? 'No system suites found')
            }
            emptyTitle={
              queryState.appliedQuery.term
                ? (t.noResultsTitle ?? 'No results')
                : (t.noSystemSuitesTitle ?? 'No system suites')
            }
            viewMode={viewMode}
            renderList={() => (
              <>
                {error && <ApiErrorBanner error={error} />}
                <div className="flex flex-col gap-0.5">
                  {systemSuites.map(renderSystemSuiteRow)}
                </div>
              </>
            )}
            renderThumbnail={() => (
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                {systemSuites.map(renderSystemSuiteCard)}
              </div>
            )}
            pagination={pagination}
            footerElement={footerTelemetry}
          />
        )
      }
    />
  );
};
