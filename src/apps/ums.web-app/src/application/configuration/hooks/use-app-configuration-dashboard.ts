/**
 * useAppConfigurationDashboard — orchestrates state for the app configuration dashboard.
 */
import { useState, useCallback } from 'react';
import {
  useGetAllAppConfigurations,
  useGetAppConfigurationById,
} from '@app/configuration/hooks/use-app-configuration';
import type { AppConfiguration } from '@domain/configuration/schemas/app-configuration.schema';
import { useQueryState } from '@app/shared/hooks/use-query-state';
import { usePaginationState, type PageSizeOption } from '@app/shared/hooks/use-pagination-state';

export interface AppConfigurationDashboardOptions {
  initialCriteria?: string;
  initialFilter?: 'all' | 'Draft' | 'Published' | 'Archived' | 'Global' | 'Tenant';
  initialSortBy?: 'code' | 'scope' | 'status';
  initialAppliedFilter?: boolean;
  fixedScope?: 'Global' | 'Tenant' | 'Suite' | 'Module';
  initialPageSize?: PageSizeOption;
  enabled?: boolean;
}

export function useAppConfigurationDashboard(options: AppConfigurationDashboardOptions = {}) {
  const [selectedId, setSelectedId] = useState('');
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');

  const queryState = useQueryState({
    criteria: options.initialCriteria ?? 'code',
    filter: options.initialFilter ?? 'all',
    sortBy: options.initialSortBy ?? 'code',
    appliedFilter: options.initialAppliedFilter ?? false,
  });

  const paginationState = usePaginationState({
    initialPageSize: options.initialPageSize ?? 10,
  });

  const isEnabled = options.enabled ?? true;
  const shouldFetch = queryState.appliedQuery.filterApplied;
  const fixedScope = options.fixedScope;
  const activeScope =
    fixedScope ?? (queryState.activeFilter !== 'all' ? queryState.activeFilter : undefined);

  const {
    data: pageData,
    isLoading: isLoadingList,
    error: listError,
  } = useGetAllAppConfigurations(
    isEnabled && shouldFetch
      ? {
          page: paginationState.page,
          pageSize: paginationState.pageSize,
          search: queryState.appliedQuery.term || undefined,
          status: queryState.activeFilter !== 'all' ? queryState.activeFilter : undefined,
          sortBy: queryState.sortBy,
          sortOrder: queryState.sortOrder,
          scope: activeScope,
        }
      : { page: 1, pageSize: 10 },
    isEnabled
  );

  const { data: activeConfig, isLoading: isLoadingDetail } = useGetAppConfigurationById(
    isEnabled ? selectedId || null : null
  );

  const knownConfigs: AppConfiguration[] = pageData?.items ?? [];
  const totalItems = pageData?.totalItems ?? 0;
  const totalPages = pageData?.totalPages ?? 0;

  const handleSelect = useCallback((id: string) => {
    setSelectedId(id);
  }, []);

  const handleCreateSuccess = useCallback(() => {
    setIsCreateOpen(false);
  }, []);

  return {
    selectedId,
    setSelectedId,
    isCreateOpen,
    setIsCreateOpen,
    viewMode,
    setViewMode,
    queryState,
    paginationState,

    knownConfigs,
    isLoadingList,
    listError: listError as Error | null,
    activeConfig: activeConfig ?? undefined,
    isLoadingDetail,
    totalItems,
    totalPages,

    handleSelect,
    handleCreateSuccess,
    requiresFilter: !shouldFetch,
  };
}
