/**
 * useUserAccountDashboard.ts — Orchestrates state and API calls for UserAccountDashboardScreen.
 *
 * Manages selection, search, pagination, dialogs, and mutations for the
 * UserAccount bounded context.
 */
import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  useGetAllUserAccounts,
  useActivateUserAccount,
  useBlockUserAccount,
  useRestoreUserAccount,
} from '@app/identity/hooks/use-user-account';
import { useGetAllTenants } from '@app/identity/hooks/use-tenant';
import { useLocalOverrides } from '@app/hooks/use-local-overrides';
import { useNotificationStore } from '@app/stores/notification.store';
import { useNotifiedMutation } from '@app/hooks/use-notified-mutation';
import { UserAccount } from '@domain/identity/models/user-account.model';
import { Tenant } from '@domain/identity/models/tenant.model';
import { useQueryState } from '@app/shared/hooks/use-query-state';
import { usePaginationState } from '@app/shared/hooks/use-pagination-state';
import userAccountService from '@infra/identity/services/user-account.service';

export interface UserAccountDashboardState {
  selectedId: string;
  selectedTenantId: string;
  showBlockDialog: boolean;
  showRestoreDialog: boolean;
  isCreateOpen: boolean;
  viewMode: 'list' | 'thumbnail';
  queryState: ReturnType<typeof useQueryState>;
  paginationState: ReturnType<typeof usePaginationState>;
}

export interface UserAccountDashboardActions {
  setSelectedId: React.Dispatch<React.SetStateAction<string>>;
  setSelectedTenantId: React.Dispatch<React.SetStateAction<string>>;
  setShowBlockDialog: React.Dispatch<React.SetStateAction<boolean>>;
  setShowRestoreDialog: React.Dispatch<React.SetStateAction<boolean>>;
  setIsCreateOpen: React.Dispatch<React.SetStateAction<boolean>>;
  setViewMode: React.Dispatch<React.SetStateAction<'list' | 'thumbnail'>>;
  setBlockReason: React.Dispatch<React.SetStateAction<string>>;
  handleSelectAccount: (id: string) => void;
  handleActivate: () => void;
  handleApproveAccount: (userAccountId: string) => void;
  handleBlockRequest: (userAccountId: string) => void;
  confirmBlock: () => void;
  confirmRestore: () => void;
  handleCreateSuccess: () => void;
  patchAccount: (accountId: string, patch: Partial<UserAccount>) => void;
}

export function useUserAccountDashboard(sessionTenantId?: string): UserAccountDashboardState &
  UserAccountDashboardActions & {
    knownAccounts: UserAccount[];
    isLoadingList: boolean;
    listError: Error | null;
    activeAccount: UserAccount | undefined;
    totalItems: number;
    totalPages: number;
    startIndex: number;
    tenants: Tenant[];
    requiresFilter: boolean;
  } {
  const [selectedId, setSelectedId] = useState('');
  const [selectedTenantId, setSelectedTenantId] = useState(sessionTenantId || '');
  const [showBlockDialog, setShowBlockDialog] = useState(false);
  const [showRestoreDialog, setShowRestoreDialog] = useState(false);
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [viewMode, setViewMode] = useState<'list' | 'thumbnail'>('list');
  const [blockReason, setBlockReason] = useState('');

  const queryState = useQueryState({
    criteria: 'email',
    filter: 'all',
    sortBy: 'email',
    // Patrón estándar: la lista carga al entrar (no exige aplicar un filtro primero).
    appliedFilter: true,
  });

  const paginationState = usePaginationState({
    initialPageSize: 10,
  });

  const addNotification = useNotificationStore(s => s.addNotification);

  const { data: tenantPage } = useGetAllTenants({ page: 1, pageSize: 100 });
  const tenants = useMemo(() => tenantPage?.items ?? [], [tenantPage]);

  const shouldFetch = queryState.appliedQuery.filterApplied;

  useEffect(() => {
    if (sessionTenantId && !selectedTenantId) {
      setSelectedTenantId(sessionTenantId);
    } else if (!selectedTenantId && tenants.length > 0) {
      setSelectedTenantId(tenants[0].tenantId);
    }
  }, [tenants, selectedTenantId]);

  const {
    data: accountPage,
    isLoading: isLoadingList,
    error: listError,
  } = useGetAllUserAccounts(
    shouldFetch
      ? {
          page: paginationState.page,
          pageSize: paginationState.pageSize,
          search: queryState.appliedQuery.term || undefined,
          criteria: queryState.appliedQuery.criteria,
          status: queryState.activeFilter,
          sortBy: queryState.sortBy,
          sortOrder: queryState.sortOrder,
          tenantId: selectedTenantId || undefined,
        }
      : null
  );

  const { items: knownAccounts, patchItem: patchLocalAccount } = useLocalOverrides<UserAccount>(
    accountPage?.items,
    'userAccountId'
  );

  const activeAccount = knownAccounts.find(account => account.userAccountId === selectedId);

  const activateMutation = useActivateUserAccount(selectedId);
  const blockMutation = useBlockUserAccount(selectedId);
  const restoreMutation = useRestoreUserAccount(selectedId);
  const approveMutation = useNotifiedMutation<void, string>({
    mutationFn: (userAccountId: string) => userAccountService.activateUserAccount(userAccountId),
    invalidateKeys: [['user-accounts']],
    successNotif: () => ({
      title: 'Cuenta aprobada',
      message: 'La cuenta fue aprobada y activada correctamente.',
    }),
    errorNotif: () => ({
      title: 'Error al aprobar',
      message: 'No se pudo aprobar la cuenta de usuario.',
    }),
  });

  const handleSelectAccount = useCallback((id: string) => {
    setSelectedId(id);
  }, []);

  const handleActivate = useCallback(() => {
    activateMutation.mutate(undefined, {
      onSuccess: () => {
        addNotification({
          title: 'Activated',
          message: 'User account has been activated.',
          type: 'success',
        });
      },
    });
  }, [activateMutation, addNotification]);

  const handleApproveAccount = useCallback(
    (userAccountId: string) => {
      approveMutation.mutate(userAccountId);
    },
    [approveMutation]
  );

  const handleBlockRequest = useCallback((userAccountId: string) => {
    setSelectedId(userAccountId);
    setShowBlockDialog(true);
  }, []);

  const handleRestoreRequest = useCallback((userAccountId: string) => {
    setSelectedId(userAccountId);
    setShowRestoreDialog(true);
  }, []);

  const confirmBlock = useCallback(() => {
    if (!blockReason.trim()) return;
    blockMutation.mutate(blockReason.trim(), {
      onSuccess: () => {
        addNotification({
          title: 'Blocked',
          message: 'User account has been blocked.',
          type: 'warning',
        });
        setShowBlockDialog(false);
        setBlockReason('');
      },
    });
  }, [blockMutation, blockReason, addNotification]);

  const confirmRestore = useCallback(() => {
    restoreMutation.mutate(undefined, {
      onSuccess: () => {
        addNotification({
          title: 'Restored',
          message: 'User account has been restored.',
          type: 'success',
        });
        setShowRestoreDialog(false);
      },
    });
  }, [restoreMutation, addNotification]);

  const handleCreateSuccess = useCallback(() => {
    paginationState.setPage(1);
    queryState.handleResetQuery();
    setIsCreateOpen(false);
  }, [paginationState, queryState]);

  useEffect(() => {
    if (!selectedId && knownAccounts.length > 0) {
      setSelectedId(knownAccounts[0].userAccountId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [knownAccounts]);

  const patchAccount = useCallback(
    (accountId: string, patch: Partial<UserAccount>) => {
      patchLocalAccount(accountId, patch);
    },
    [patchLocalAccount]
  );

  const totalItems = accountPage?.totalItems ?? 0;
  const totalPages = accountPage?.totalPages ?? 0;

  return {
    selectedId,
    setSelectedId,
    selectedTenantId,
    setSelectedTenantId,
    showBlockDialog,
    setShowBlockDialog,
    showRestoreDialog,
    setShowRestoreDialog,
    isCreateOpen,
    setIsCreateOpen,
    viewMode,
    setViewMode,
    queryState,
    paginationState,
    blockReason,
    setBlockReason,
    handleSelectAccount,
    handleActivate,
    handleApproveAccount,
    handleBlockRequest,
    handleRestoreRequest,
    confirmBlock,
    confirmRestore,
    handleCreateSuccess,
    patchAccount,
    knownAccounts,
    isLoadingList,
    listError,
    activeAccount,
    totalItems,
    totalPages,
    startIndex: paginationState.startIndex,
    tenants,
    requiresFilter: !shouldFetch,
  };
}
