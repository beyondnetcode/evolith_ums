/**
 * useFeatureFlagDashboard — orchestrates state for the feature flag dashboard.
 */
import { useState, useCallback } from 'react';
import {
  useGetAllFeatureFlags,
  useGetFeatureFlagById,
} from '@app/configuration/hooks/use-feature-flag';
import type { FeatureFlag } from '@domain/configuration/models/feature-flag.model';
import { useQueryState } from '@app/shared/hooks/use-query-state';
import { usePaginationState } from '@app/shared/hooks/use-pagination-state';

export function useFeatureFlagDashboard() {
  const [selectedId, setSelectedId] = useState('');
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');
  const queryState = useQueryState({
    criteria: 'flagCode',
    filter: 'all',
    sortBy: 'flagCode',
    // Patrón estándar: la lista carga al entrar (no exige aplicar un filtro primero).
    appliedFilter: true,
  });

  const paginationState = usePaginationState({
    initialPageSize: 10,
  });

  const shouldFetch = queryState.appliedQuery.filterApplied;

  const {
    data: pageData,
    isLoading: isLoadingList,
    error: listError,
  } = useGetAllFeatureFlags(
    shouldFetch
      ? {
          page: paginationState.page,
          pageSize: paginationState.pageSize,
          search: queryState.appliedQuery.term || undefined,
          status: queryState.activeFilter !== 'all' ? queryState.activeFilter : undefined,
          sortBy: queryState.sortBy,
          sortOrder: queryState.sortOrder,
        }
      : null
  );

  const { data: activeFlag, isLoading: isLoadingDetail } = useGetFeatureFlagById(
    selectedId || null
  );

  const knownFlags: FeatureFlag[] = pageData?.items ?? [];
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

    knownFlags,
    isLoadingList,
    listError: listError as Error | null,
    activeFlag: activeFlag ?? undefined,
    isLoadingDetail,
    totalItems,
    totalPages,

    handleSelect,
    handleCreateSuccess,
    requiresFilter: !shouldFetch,
  };
}
