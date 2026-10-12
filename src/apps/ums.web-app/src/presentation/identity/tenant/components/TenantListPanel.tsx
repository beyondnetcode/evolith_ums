import React, { useCallback } from 'react';
import { Info, LayoutList, LayoutGrid } from 'lucide-react';
import { ListToolbar } from '@shared/components/ListToolbar';
import { Tenant } from '@domain/identity/models/tenant.model';
import { HierarchicalList } from '@shared/components/HierarchicalList';
import {
  renderTenantParentRow,
  renderTenantChildRow,
  renderTenantParentCard,
  renderTenantChildCard,
} from './tenant-list-renders';
import type { TreeNode } from '@app/hooks/use-tree-nodes';
import {
  DataViewShell,
  DataList,
  AtomicQueryCriteriaOption,
  AtomicFilterOption,
  AtomicSortOption,
  PaginationFooter,
  RequiresFilterPrompt,
} from '@shared/components';
import { useQueryState } from '@app/shared/hooks/use-query-state';
import { usePaginationState } from '@app/shared/hooks/use-pagination-state';
import { ApiErrorBanner } from '@shared/components/ApiErrorBanner';
import { useI18n } from '@app/i18n/use-i18n';
import { useStatusLabel } from '@app/hooks/use-status-label';

interface TenantListPanelProps {
  tenants: Tenant[];
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
  onAddDisabled?: boolean;
  onSelectTenant: (tenantId: string) => void;
  criteriaOptions: AtomicQueryCriteriaOption[];
  filterOptions: AtomicFilterOption[];
  sortOptions: AtomicSortOption[];
  requiresFilter?: boolean;
}

export const TenantListPanel: React.FC<TenantListPanelProps> = ({
  tenants,
  selectedId,
  isLoading,
  error,
  viewMode,
  onViewModeChange,
  queryState,
  paginationState,
  onRegisterNew,
  onAddDisabled,
  onSelectTenant,
  criteriaOptions,
  filterOptions,
  sortOptions,
  requiresFilter = false,
}) => {
  const t = useI18n();
  const statusLabel = useStatusLabel();

  const renderParentRow = useCallback(
    (node: TreeNode<Tenant>, isSelected: boolean, isExpanded: boolean, onToggle: () => void) =>
      renderTenantParentRow(node, isSelected, isExpanded, onToggle, onSelectTenant, statusLabel, t),
    [onSelectTenant, statusLabel, t]
  );

  const renderChildRow = useCallback(
    (child: Tenant, isChildSelected: boolean) =>
      renderTenantChildRow(child, isChildSelected, onSelectTenant, statusLabel, t),
    [onSelectTenant, statusLabel, t]
  );

  const renderParentCard = useCallback(
    (node: TreeNode<Tenant>, isSelected: boolean, isExpanded: boolean, onToggle: () => void) =>
      renderTenantParentCard(
        node,
        isSelected,
        isExpanded,
        onToggle,
        onSelectTenant,
        statusLabel,
        t
      ),
    [onSelectTenant, statusLabel, t]
  );

  const renderChildCard = useCallback(
    (child: Tenant, isChildSelected: boolean) =>
      renderTenantChildCard(child, isChildSelected, onSelectTenant, statusLabel, t),
    [onSelectTenant, statusLabel, t]
  );

  const totalItems = paginationState.totalItems;
  const startIndex = paginationState.startIndex ?? 0;
  const pageSize = paginationState.pageSize;

  const footerTelemetry = (
    <div className="flex items-center gap-3">
      <div className="flex items-center gap-1.5">
        <span className="h-2 w-2 rounded-full bg-m3-primary animate-pulse" />
        <span className="text-[12px] font-medium text-m3-secondary/80">
          {t.showing} {totalItems === 0 ? 0 : startIndex + 1}-
          {Math.min(startIndex + pageSize, totalItems)} {t.of} {totalItems} {t.tenants}
        </span>
      </div>
      {queryState.appliedQuery.term.trim() && (
        <button
          onClick={queryState.handleResetQuery}
          className="text-xs font-medium text-rose-500 hover:underline flex items-center gap-1"
        >
          <Info className="w-3 h-3" /> {t.clearFilter}
        </button>
      )}
    </div>
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
    <RequiresFilterPrompt title={t.applyFilterTitle} message={t.applyFilterMessage} />
  ) : null;

  return (
    <DataViewShell
      title={t.tenants}
      subtitle={t.tenantManagementSubtitle}
      controls={
        <ListToolbar
          itemCount={totalItems}
          itemLabel="tenant"
          itemLabelPlural="tenants"
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
          onAddDisabled={onAddDisabled}
        />
      }
      content={
        requiresFilter ? (
          filterPrompt
        ) : (
          <DataList
            isLoading={isLoading}
            isEmpty={totalItems === 0}
            emptyLabel={t.noRecords}
            emptyTitle={t.dataViewEmptyTitle}
            viewMode={viewMode}
            renderList={() => (
              <>
                {error && <ApiErrorBanner error={error} />}
                <div className="flex flex-col gap-0.5">
                  <HierarchicalList<Tenant>
                    items={tenants}
                    idKey="tenantId"
                    parentIdKey="parentTenantId"
                    selectedId={selectedId}
                    onSelect={onSelectTenant}
                    renderParentRow={renderParentRow}
                    renderChildRow={renderChildRow}
                    renderParentCard={renderParentCard}
                    renderChildCard={renderChildCard}
                    viewMode={viewMode}
                  />
                </div>
              </>
            )}
            renderThumbnail={() => (
              <HierarchicalList<Tenant>
                items={tenants}
                idKey="tenantId"
                parentIdKey="parentTenantId"
                selectedId={selectedId}
                onSelect={onSelectTenant}
                renderParentRow={renderParentRow}
                renderChildRow={renderChildRow}
                renderParentCard={renderParentCard}
                renderChildCard={renderChildCard}
                viewMode={viewMode}
              />
            )}
            pagination={pagination}
            footerElement={footerTelemetry}
          />
        )
      }
    />
  );
};
