import React, { useCallback } from 'react';
import { Shield, ArrowRight, Info } from 'lucide-react';
import type { Delegation } from '@domain/identity/models/delegation.model';
import { StatusBadge } from '@shared/components/StatusBadge';
import { CodeBadge } from '@shared/components/CodeBadge';
import { EntityRow } from '@shared/components/EntityRow';
import { useI18n } from '@app/i18n/use-i18n';
import { useStatusLabel } from '@app/hooks/use-status-label';
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
import { ApiErrorBanner } from '@shared/components/ApiErrorBanner';
import { DelegationViewType } from '@app/identity/hooks/use-delegation-dashboard';
import { Tooltip } from '@shared/components/Tooltip';

interface DelegationListPanelProps {
  delegations: Delegation[];
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
  onSelectDelegation: (delegationId: string) => void;
  criteriaOptions: AtomicQueryCriteriaOption[];
  filterOptions: AtomicFilterOption[];
  sortOptions: AtomicSortOption[];
  delegationViewType: DelegationViewType;
  onDelegationViewTypeChange: (type: DelegationViewType) => void;
  requiresFilter?: boolean;
}

export const DelegationListPanel: React.FC<DelegationListPanelProps> = ({
  delegations,
  selectedId,
  isLoading,
  error,
  viewMode,
  onViewModeChange,
  queryState,
  paginationState,
  onRegisterNew,
  onSelectDelegation,
  criteriaOptions,
  filterOptions,
  sortOptions,
  delegationViewType,
  onDelegationViewTypeChange,
  requiresFilter = false,
}) => {
  const t = useI18n();
  const getStatusLabel = useStatusLabel();

  const renderDelegationRow = useCallback(
    (delegation: Delegation) => {
      const isSelected = delegation.delegationId === selectedId;
      return (
        <EntityRow
          key={delegation.delegationId}
          id={delegation.delegationId}
          selected={isSelected}
          onClick={() => onSelectDelegation(delegation.delegationId)}
          leading={
            <div
              className={`p-2 rounded-lg transition-colors ${isSelected ? 'bg-m3-primary/15' : 'bg-m3-surface-container/50'}`}
            >
              <Shield
                className={`w-4 h-4 ${isSelected ? 'text-m3-primary' : 'text-m3-secondary'}`}
              />
            </div>
          }
          trailingColumns={[
            { content: <CodeBadge code={delegation.scopeType} />, width: 'w-24' },
            {
              content: (
                <StatusBadge status={delegation.status} label={getStatusLabel(delegation.status)} />
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
          <div>
            <span className="text-sm font-semibold text-m3-on-surface">Delegación de Acceso</span>
            <span className="ml-2 text-[10px] text-m3-secondary/70 font-mono">
              {delegation.scopeType}
            </span>
          </div>
        </EntityRow>
      );
    },
    [selectedId, onSelectDelegation, getStatusLabel]
  );

  const renderDelegationCard = useCallback(
    (delegation: Delegation) => {
      const isSelected = delegation.delegationId === selectedId;
      return (
        <div
          key={delegation.delegationId}
          onClick={() => onSelectDelegation(delegation.delegationId)}
          className={`p-4 rounded-xl border cursor-pointer transition-colors ${
            isSelected
              ? 'border-m3-primary bg-m3-primary/5'
              : 'border-m3-outline/20 bg-m3-surface hover:bg-m3-surface-container'
          }`}
        >
          <div className="flex items-start justify-between mb-3">
            <div className="flex items-center gap-2">
              <Shield
                className={`w-5 h-5 ${isSelected ? 'text-m3-primary' : 'text-m3-secondary'}`}
              />
              <span className="text-sm font-medium">Delegación de Acceso</span>
            </div>
            <StatusBadge status={delegation.status} label={getStatusLabel(delegation.status)} />
          </div>
          <div className="flex items-center gap-2 mt-2">
            <span className="text-xs text-m3-on-surface-variant">Scope:</span>
            <CodeBadge code={delegation.scopeType} />
          </div>
        </div>
      );
    },
    [selectedId, onSelectDelegation, getStatusLabel]
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
      message="Selecciona un estado o ingresa un término de búsqueda para visualizar las delegaciones."
    />
  ) : null;

  const footerTelemetry = (
    <PaginationFooter
      totalItems={paginationState.totalItems}
      startIndex={paginationState.startIndex ?? 0}
      pageSize={paginationState.pageSize}
      itemLabel="delegaciones"
      onClear={queryState.handleResetQuery}
      searchTerm={queryState.appliedQuery.term}
    />
  );

  return (
    <div className="flex flex-col h-full">
      <div className="flex border-b border-m3-outline/15 px-4 pt-2 mb-2 bg-m3-surface-container/10">
        <Tooltip content="Delegaciones que has recibido (donde tú eres el administrador delegado).">
          <button
            onClick={() => onDelegationViewTypeChange('received')}
            className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${delegationViewType === 'received' ? 'border-m3-primary text-m3-primary' : 'border-transparent text-m3-on-surface-variant hover:text-m3-on-surface'}`}
          >
            Received Delegations
          </button>
        </Tooltip>
        <Tooltip content="Delegaciones que has otorgado a otros (donde tú eres el administrador delegador).">
          <button
            onClick={() => onDelegationViewTypeChange('granted')}
            className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${delegationViewType === 'granted' ? 'border-m3-primary text-m3-primary' : 'border-transparent text-m3-on-surface-variant hover:text-m3-on-surface'}`}
          >
            Granted Delegations
          </button>
        </Tooltip>
      </div>
      <div className="flex-1 min-h-0">
        <DataViewShell
          title={'Delegation Management'}
          subtitle={
            delegationViewType === 'received'
              ? 'Delegations granted to you'
              : 'Delegations you have granted to others'
          }
          controls={
            <ListToolbar
              itemCount={totalItems}
              itemLabel="delegación"
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
                emptyLabel={t.noRecords ?? 'No records found'}
                emptyTitle={t.dataViewEmptyTitle ?? 'No Results'}
                viewMode={viewMode}
                renderList={() => (
                  <>
                    {error && <ApiErrorBanner error={error} />}
                    <div className="overflow-x-auto border border-m3-outline/25 rounded-xl bg-m3-surface-container/20">
                      <div className="flex flex-col gap-0.5 text-sm p-1">
                        {delegations.map(renderDelegationRow)}
                      </div>
                    </div>
                  </>
                )}
                renderThumbnail={() => (
                  <>
                    {error && <ApiErrorBanner error={error} />}
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                      {delegations.map(renderDelegationCard)}
                    </div>
                  </>
                )}
                pagination={pagination}
                footerElement={footerTelemetry}
              />
            )
          }
        />
      </div>
    </div>
  );
};
