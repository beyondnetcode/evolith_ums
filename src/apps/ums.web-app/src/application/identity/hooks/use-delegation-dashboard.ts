import React, { useState, useEffect, useCallback } from 'react';
import {
  useGetDelegationsByDelegatedAdmin,
  useGetDelegationsByDelegatingAdmin,
} from './use-delegation';
import { Delegation } from '@domain/identity/models/delegation.model';
import { useLocalOverrides } from '@app/hooks/use-local-overrides';
import { useQueryState } from '@app/shared/hooks/use-query-state';
import { usePaginationState } from '@app/shared/hooks/use-pagination-state';
import { useAuthStore } from '@app/stores/auth.store';

export type DelegationViewType = 'received' | 'granted';

export interface DelegationDashboardState {
  selectedId: string;
  showDiscardDialog: boolean;
  pendingNavigationId: string | null;
  activeConsoleTab: 'overview' | 'permissions';
  isEditing: boolean;
  isCreateOpen: boolean;
  viewMode: 'list' | 'thumbnail';
  queryState: ReturnType<typeof useQueryState<string, string>>;
  paginationState: ReturnType<typeof usePaginationState>;
  delegationViewType: DelegationViewType;
}

export interface DelegationDashboardActions {
  setSelectedId: React.Dispatch<React.SetStateAction<string>>;
  setShowDiscardDialog: React.Dispatch<React.SetStateAction<boolean>>;
  setPendingNavigationId: React.Dispatch<React.SetStateAction<string | null>>;
  setActiveConsoleTab: React.Dispatch<React.SetStateAction<'overview' | 'permissions'>>;
  setIsEditing: React.Dispatch<React.SetStateAction<boolean>>;
  setIsCreateOpen: React.Dispatch<React.SetStateAction<boolean>>;
  setViewMode: React.Dispatch<React.SetStateAction<'list' | 'thumbnail'>>;
  setDelegationViewType: React.Dispatch<React.SetStateAction<DelegationViewType>>;
  handleSelectDelegation: (id: string) => void;
  confirmDiscard: () => void;
  patchDelegation: (id: string, patch: Partial<Delegation>) => void;
  handleCreateSuccess: () => void;
}

export function useDelegationDashboard(): DelegationDashboardState &
  DelegationDashboardActions & {
    knownDelegations: Delegation[];
    isLoadingList: boolean;
    listError: Error | null;
    activeDelegation: Delegation | undefined;
    consoleTabs: Array<'overview' | 'permissions'>;
    totalItems: number;
    totalPages: number;
    startIndex: number;
    currentUserId: string;
    currentTenantId: string;
  } {
  const { user } = useAuthStore();
  const currentUserId = user?.id ?? '';
  const currentTenantId = user?.tenantId ?? '';

  const [selectedId, setSelectedId] = useState('');
  const [showDiscardDialog, setShowDiscardDialog] = useState(false);
  const [pendingNavigationId, setPendingNavigationId] = useState<string | null>(null);
  const [activeConsoleTab, setActiveConsoleTab] = useState<'overview' | 'permissions'>('overview');
  const [isEditing, setIsEditing] = useState(false);
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');
  const [delegationViewType, setDelegationViewType] = useState<DelegationViewType>('received');

  const queryState = useQueryState({
    criteria: 'id',
    filter: 'all',
    sortBy: 'status',
    // Patrón estándar: la lista carga al entrar (no exige aplicar un filtro primero).
    appliedFilter: true,
  });

  const paginationState = usePaginationState({
    initialPageSize: 10,
  });

  const shouldFetch = queryState.appliedQuery.filterApplied && !!currentTenantId;

  const receivedQuery = useGetDelegationsByDelegatedAdmin(currentUserId, currentTenantId);
  const grantedQuery = useGetDelegationsByDelegatingAdmin(currentUserId, currentTenantId);

  const isReceived = delegationViewType === 'received';
  const activeQuery = isReceived ? receivedQuery : grantedQuery;

  let filteredItems = shouldFetch ? (activeQuery.data ?? []) : [];

  if (queryState.appliedQuery.term) {
    const term = queryState.appliedQuery.term.toLowerCase();
    filteredItems = filteredItems.filter(
      d => d.delegationId.toLowerCase().includes(term) || d.scopeType.toLowerCase().includes(term)
    );
  }

  if (queryState.activeFilter !== 'all') {
    filteredItems = filteredItems.filter(d => d.status === queryState.activeFilter);
  }

  const { items: knownDelegations, patchItem: patchLocalDelegation } =
    useLocalOverrides<Delegation>(filteredItems, 'delegationId');

  const activeDelegation = knownDelegations.find(d => d.delegationId === selectedId);
  const consoleTabs: Array<'overview' | 'permissions'> = ['overview', 'permissions'];
  const hasPendingChanges = isEditing;

  const applyDelegationSelection = useCallback((id: string) => {
    setIsEditing(false);
    setActiveConsoleTab('overview');
    setSelectedId(id);
  }, []);

  const handleSelectDelegation = useCallback(
    (id: string) => {
      if (id === selectedId) return;
      if (hasPendingChanges) {
        setPendingNavigationId(id);
        setShowDiscardDialog(true);
        return;
      }
      applyDelegationSelection(id);
    },
    [selectedId, hasPendingChanges, applyDelegationSelection]
  );

  const confirmDiscard = useCallback(() => {
    if (pendingNavigationId) applyDelegationSelection(pendingNavigationId);
    setPendingNavigationId(null);
    setShowDiscardDialog(false);
  }, [pendingNavigationId, applyDelegationSelection]);

  useEffect(() => {
    if (!selectedId && knownDelegations.length > 0) {
      applyDelegationSelection(knownDelegations[0].delegationId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [knownDelegations]);

  const patchDelegation = useCallback(
    (id: string, patch: Partial<Delegation>) => {
      patchLocalDelegation(id, patch);
    },
    [patchLocalDelegation]
  );

  const handleCreateSuccess = useCallback(() => {
    setIsCreateOpen(false);
    setDelegationViewType('granted');
    paginationState.setPage(1);
    queryState.setSearchValue('');
    queryState.handleResetQuery();
  }, [paginationState, queryState]);

  const totalItems = knownDelegations.length;
  const totalPages = Math.ceil(totalItems / paginationState.pageSize) || 1;
  const startIndex = (paginationState.page - 1) * paginationState.pageSize;

  const paginatedDelegations = knownDelegations.slice(
    startIndex,
    startIndex + paginationState.pageSize
  );

  return {
    selectedId,
    setSelectedId,
    showDiscardDialog,
    setShowDiscardDialog,
    pendingNavigationId,
    setPendingNavigationId,
    activeConsoleTab,
    setActiveConsoleTab,
    isEditing,
    setIsEditing,
    isCreateOpen,
    setIsCreateOpen,
    viewMode,
    setViewMode,
    queryState,
    paginationState,
    delegationViewType,
    setDelegationViewType,
    handleSelectDelegation,
    confirmDiscard,
    patchDelegation,
    handleCreateSuccess,
    knownDelegations: paginatedDelegations,
    isLoadingList: activeQuery.isLoading,
    listError: activeQuery.error ?? null,
    activeDelegation,
    consoleTabs,
    totalItems,
    totalPages,
    startIndex,
    currentUserId,
    currentTenantId,
    requiresFilter: !shouldFetch,
  };
}
