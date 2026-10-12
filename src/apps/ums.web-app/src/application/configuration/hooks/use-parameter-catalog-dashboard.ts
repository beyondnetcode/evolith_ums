/**
 * useParameterCatalogDashboard — orchestrates state for the parameter catalog dashboard.
 */
import { useState, useCallback } from 'react';
import {
  useParameterDefinitions,
  useParameterDefinitionById,
  useCreateParameterDefinition,
  useUpdateParameterDefinition,
  useDeleteParameterDefinition,
} from '@app/configuration/parameter-catalog/use-parameter-catalog';
import type { ParameterDefinition } from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';
import type {
  CreateParameterDefinitionPayload,
  UpdateParameterDefinitionPayload,
} from '@domain/configuration/schemas/parameter-catalog/parameter-definition.schema';
import { useQueryState } from '@app/shared/hooks/use-query-state';
import { usePaginationState } from '@app/shared/hooks/use-pagination-state';

export function useParameterCatalogDashboard() {
  const [selectedId, setSelectedId] = useState('');
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');

  const queryState = useQueryState({
    criteria: 'code',
    filter: 'all',
    sortBy: 'code',
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
  } = useParameterDefinitions(
    shouldFetch
      ? {
          search: queryState.appliedQuery.term || undefined,
          scopeId: queryState.activeFilter !== 'all' ? Number(queryState.activeFilter) : undefined,
        }
      : undefined
  );

  const { data: activeParameter, isLoading: isLoadingDetail } = useParameterDefinitionById(
    selectedId || ''
  );

  const createMutation = useCreateParameterDefinition();
  const updateMutation = useUpdateParameterDefinition();
  const deleteMutation = useDeleteParameterDefinition();

  const knownParameters: ParameterDefinition[] = pageData?.items ?? [];
  const totalItems = pageData?.totalItems ?? 0;
  const totalPages = pageData?.totalPages ?? 1;

  const handleSelect = useCallback((id: string) => {
    setSelectedId(id);
    setIsEditOpen(false);
  }, []);

  const handleCreateSuccess = useCallback(
    (newId: string) => {
      setIsCreateOpen(false);
      setSelectedId(newId);
      paginationState.setPage(1);
      queryState.handleResetQuery();
    },
    [paginationState, queryState]
  );

  const handleUpdateSuccess = useCallback((updatedId: string) => {
    setIsEditOpen(false);
    setSelectedId(updatedId);
  }, []);

  const handleDeleteConfirm = useCallback(async () => {
    if (pendingDeleteId) {
      try {
        await deleteMutation.mutateAsync(pendingDeleteId);
        setIsDeleteDialogOpen(false);
        setPendingDeleteId(null);
        if (selectedId === pendingDeleteId) {
          setSelectedId('');
        }
      } catch {
        // Error handled by mutation
      }
    }
  }, [pendingDeleteId, selectedId, deleteMutation]);

  const handleDeleteRequest = useCallback((id: string) => {
    setPendingDeleteId(id);
    setIsDeleteDialogOpen(true);
  }, []);

  const handleEditRequest = useCallback(() => {
    if (activeParameter) {
      setIsEditOpen(true);
    }
  }, [activeParameter]);

  const createParameter = useCallback(
    async (payload: CreateParameterDefinitionPayload) => {
      const result = await createMutation.mutateAsync(payload);
      handleCreateSuccess(result.id);
      return result;
    },
    [createMutation, handleCreateSuccess]
  );

  const updateParameter = useCallback(
    async (id: string, payload: UpdateParameterDefinitionPayload) => {
      const result = await updateMutation.mutateAsync({ id, payload });
      handleUpdateSuccess(result.id);
      return result;
    },
    [updateMutation, handleUpdateSuccess]
  );

  return {
    selectedId,
    setSelectedId,
    isCreateOpen,
    setIsCreateOpen,
    isEditOpen,
    setIsEditOpen,
    isDeleteDialogOpen,
    setIsDeleteDialogOpen,
    pendingDeleteId,
    viewMode,
    setViewMode,
    queryState,
    paginationState,

    knownParameters,
    isLoadingList,
    isLoadingDetail,
    listError: listError as Error | null,
    activeParameter: activeParameter ?? undefined,

    totalItems,
    totalPages,

    handleSelect,
    handleCreateSuccess,
    handleUpdateSuccess,
    handleDeleteRequest,
    handleDeleteConfirm,

    handleEditRequest,
    createParameter,
    updateParameter,
    deleteParameter: handleDeleteRequest,

    isCreating: createMutation.isPending,
    isUpdating: updateMutation.isPending,
    isDeleting: deleteMutation.isPending,

    requiresFilter: false,
  };
}
