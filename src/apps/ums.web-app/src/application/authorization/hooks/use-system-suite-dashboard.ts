import React, { useState, useEffect, useCallback } from 'react';
import { useGetAllSystemSuites } from '@app/authorization/hooks/use-system-suite';
import { useLocalOverrides } from '@app/hooks/use-local-overrides';
import { SystemSuite } from '@domain/authorization/models/system-suite.model';
import { useQueryState } from '@app/shared/hooks/use-query-state';
import { usePaginationState } from '@app/shared/hooks/use-pagination-state';

export interface SystemSuiteDashboardState {
  selectedId: string;
  showDiscardDialog: boolean;
  pendingNavigationId: string | null;
  isEditing: boolean;
  isCreateOpen: boolean;
  viewMode: 'list' | 'thumbnail';
  queryState: ReturnType<typeof useQueryState<string, string>>;
  paginationState: ReturnType<typeof usePaginationState>;
}

export interface SystemSuiteDashboardActions {
  setSelectedId: React.Dispatch<React.SetStateAction<string>>;
  setShowDiscardDialog: React.Dispatch<React.SetStateAction<boolean>>;
  setPendingNavigationId: React.Dispatch<React.SetStateAction<string | null>>;
  setIsEditing: React.Dispatch<React.SetStateAction<boolean>>;
  setIsCreateOpen: React.Dispatch<React.SetStateAction<boolean>>;
  setViewMode: React.Dispatch<React.SetStateAction<'list' | 'thumbnail'>>;

  handleSelectSystemSuite: (id: string) => void;
  confirmDiscard: () => void;
  patchSystemSuite: (id: string, patch: Partial<SystemSuite>) => void;
  handleCreateSuccess: () => void;
}

export function useSystemSuiteDashboard(): SystemSuiteDashboardState &
  SystemSuiteDashboardActions & {
    knownSystemSuites: SystemSuite[];
    isLoadingList: boolean;
    listError: Error | null;
    activeSystemSuite: SystemSuite | undefined;
    totalItems: number;
    totalPages: number;
    startIndex: number;
    requiresFilter: boolean;
  } {
  const [selectedId, setSelectedId] = useState('');
  const [showDiscardDialog, setShowDiscardDialog] = useState(false);
  const [pendingNavigationId, setPendingNavigationId] = useState<string | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');

  const queryState = useQueryState({
    criteria: 'name',
    filter: 'all',
    sortBy: 'name',
  });

  const paginationState = usePaginationState({
    initialPageSize: 10,
  });

  // System suites are few in number — load automatically without requiring a filter first
  const shouldFetch = true;

  const {
    data: systemSuitePage,
    isLoading: isLoadingList,
    error: listError,
  } = useGetAllSystemSuites(
    shouldFetch
      ? {
          page: paginationState.page,
          pageSize: paginationState.pageSize,
          search: queryState.appliedQuery.term,
          criteria: queryState.appliedQuery.criteria,
          status: queryState.activeFilter,
          sortBy: queryState.sortBy,
          sortOrder: queryState.sortOrder,
        }
      : null
  );

  const { items: knownSystemSuites, patchItem: patchLocalSystemSuite } =
    useLocalOverrides<SystemSuite>(systemSuitePage?.items, 'systemSuiteId');

  const activeSystemSuite = knownSystemSuites.find(s => s.systemSuiteId === selectedId);

  const hasPendingChanges = isEditing;

  const applySystemSuiteSelection = useCallback((id: string) => {
    setIsEditing(false);
    setSelectedId(id);
  }, []);

  const handleSelectSystemSuite = useCallback(
    (id: string) => {
      if (id === selectedId) return;
      if (hasPendingChanges) {
        setPendingNavigationId(id);
        setShowDiscardDialog(true);
        return;
      }
      applySystemSuiteSelection(id);
    },
    [selectedId, hasPendingChanges, applySystemSuiteSelection]
  );

  const confirmDiscard = useCallback(() => {
    if (pendingNavigationId) applySystemSuiteSelection(pendingNavigationId);
    setPendingNavigationId(null);
    setShowDiscardDialog(false);
  }, [pendingNavigationId, applySystemSuiteSelection]);

  useEffect(() => {
    if (!selectedId && knownSystemSuites.length > 0) {
      applySystemSuiteSelection(knownSystemSuites[0].systemSuiteId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [knownSystemSuites]);

  const patchSystemSuite = useCallback(
    (id: string, patch: Partial<SystemSuite>) => {
      patchLocalSystemSuite(id, patch);
    },
    [patchLocalSystemSuite]
  );

  const handleCreateSuccess = useCallback(() => {
    paginationState.setPage(1);
    queryState.setSearchValue('');
    queryState.handleResetQuery();
    setIsCreateOpen(false);
  }, [paginationState, queryState]);

  const totalItems = systemSuitePage?.totalItems ?? 0;
  const totalPages = systemSuitePage?.totalPages ?? 0;
  const startIndex = (paginationState.page - 1) * paginationState.pageSize;

  return {
    selectedId,
    setSelectedId,
    showDiscardDialog,
    setShowDiscardDialog,
    pendingNavigationId,
    setPendingNavigationId,
    isEditing,
    setIsEditing,
    isCreateOpen,
    setIsCreateOpen,
    viewMode,
    setViewMode,
    queryState,
    paginationState,
    handleSelectSystemSuite,
    confirmDiscard,
    patchSystemSuite,
    handleCreateSuccess,
    knownSystemSuites,
    isLoadingList,
    listError,
    activeSystemSuite,
    totalItems,
    totalPages,
    startIndex,
    requiresFilter: !shouldFetch,
  };
}
