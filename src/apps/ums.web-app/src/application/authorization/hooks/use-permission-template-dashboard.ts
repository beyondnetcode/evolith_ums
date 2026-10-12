/**
 * usePermissionTemplateDashboard — orchestrates state for the template dashboard.
 */
import { useState, useCallback } from 'react';
import { useGetAllPermissionTemplates } from './use-permission-template';
import { useGetPermissionTemplate } from './use-permission-template';
import type { PermissionTemplate } from '@domain/authorization/models/permission-template.model';
import { useQueryState } from '@app/shared/hooks/use-query-state';
import { usePaginationState } from '@app/shared/hooks/use-pagination-state';

export function usePermissionTemplateDashboard(tenantId?: string) {
  const [selectedId, setSelectedId] = useState('');
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');

  const queryState = useQueryState({
    criteria: 'role',
    filter: 'all',
    sortBy: 'suite',
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
  } = useGetAllPermissionTemplates(
    shouldFetch
      ? {
          page: paginationState.page,
          pageSize: paginationState.pageSize,
          search: queryState.appliedQuery.term || undefined,
          criteria: queryState.appliedQuery.criteria || 'version',
          status: queryState.activeFilter !== 'all' ? queryState.activeFilter : undefined,
          sortBy: queryState.sortBy,
          sortOrder: queryState.sortOrder,
          tenantId,
        }
      : null
  );

  const { data: activeTemplate, isLoading: isLoadingDetail } = useGetPermissionTemplate(
    selectedId || null
  );

  const knownTemplates: PermissionTemplate[] = pageData?.items ?? [];
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

    knownTemplates,
    isLoadingList,
    listError: listError as Error | null,
    activeTemplate: activeTemplate ?? undefined,
    isLoadingDetail,
    totalItems,
    totalPages,

    handleSelect,
    handleCreateSuccess,
    requiresFilter: !shouldFetch,
  };
}
