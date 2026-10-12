import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import type { ReactNode } from 'react';
import { useUserAccountDashboard } from './use-user-account-dashboard';
import * as useUserAccountModule from '@app/identity/hooks/use-user-account';
import * as useTenantModule from '@app/identity/hooks/use-tenant';
import * as useLocalOverridesModule from '@app/hooks/use-local-overrides';
import * as useQueryStateModule from '@app/shared/hooks/use-query-state';
import * as usePaginationStateModule from '@app/shared/hooks/use-pagination-state';
import * as notificationStoreModule from '@app/stores/notification.store';

vi.mock('@app/identity/hooks/use-user-account');
vi.mock('@app/identity/hooks/use-tenant');
vi.mock('@app/hooks/use-local-overrides');
vi.mock('@app/shared/hooks/use-query-state');
vi.mock('@app/shared/hooks/use-pagination-state');
vi.mock('@app/stores/notification.store');
vi.mock('@domain/identity/constants/user-account.constants', () => ({
  USER_ACCOUNT_PAGE_SIZE: 20,
}));

const mockAccounts = [
  {
    userAccountId: 'u-1',
    displayName: 'Admin One',
    email: 'user1@test.com',
    category: 'Internal',
    status: 'Active',
    tenantId: 't-1',
    branchId: null,
    identityReference: null,
    identityReferenceType: null,
    hasActivePassword: true,
    passwordUpdatedAtUtc: null,
  },
  {
    userAccountId: 'u-2',
    displayName: 'Pending User',
    email: 'user2@test.com',
    category: 'External',
    status: 'Pending',
    tenantId: 't-1',
    branchId: null,
    identityReference: null,
    identityReferenceType: null,
    hasActivePassword: false,
    passwordUpdatedAtUtc: null,
  },
];

const mockTenants = [
  {
    tenantId: 't-1',
    code: 'T1',
    name: 'Tenant 1',
    type: 'INTERNAL',
    status: 'Active',
    parentTenantId: null,
    companyReference: null,
  },
];

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return function Wrapper({ children }: { children: ReactNode }) {
    return React.createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

describe('useUserAccountDashboard', () => {
  beforeEach(() => {
    vi.restoreAllMocks();

    vi.mocked(useUserAccountModule.useGetAllUserAccounts).mockReturnValue({
      data: { items: mockAccounts, page: 1, pageSize: 20, totalItems: 2, totalPages: 1 },
      isLoading: false,
      error: null,
    } as any);

    vi.mocked(useTenantModule.useGetAllTenants).mockReturnValue({
      data: { items: mockTenants, page: 1, pageSize: 100, totalItems: 1, totalPages: 1 },
      isLoading: false,
      error: null,
    } as any);

    vi.mocked(useLocalOverridesModule.useLocalOverrides).mockReturnValue({
      items: mockAccounts,
      patchItem: vi.fn(),
      patchItems: vi.fn(),
      clearOverrides: vi.fn(),
      rollbackItem: vi.fn(),
      rollbackAll: vi.fn(),
      getDiffs: vi.fn(() => []),
      isDirty: vi.fn(() => false),
    });

    vi.mocked(useQueryStateModule.useQueryState).mockReturnValue({
      searchCriteria: 'email',
      setSearchCriteria: vi.fn(),
      searchValue: '',
      setSearchValue: vi.fn(),
      activeFilter: 'all',
      setActiveFilter: vi.fn(),
      sortBy: 'email',
      setSortBy: vi.fn(),
      sortOrder: 'asc',
      setSortOrder: vi.fn(),
      toggleSortOrder: vi.fn(),
      appliedQuery: { criteria: 'email', term: '' },
      handleQuerySubmit: vi.fn(),
      handleResetQuery: vi.fn(),
    } as any);

    vi.mocked(usePaginationStateModule.usePaginationState).mockReturnValue({
      page: 1,
      setPage: vi.fn(),
      pageSize: 20,
      setPageSize: vi.fn(),
      startIndex: 0,
      handlePageChange: vi.fn(),
      handlePageSizeChange: vi.fn(),
    } as any);

    vi.mocked(notificationStoreModule.useNotificationStore).mockReturnValue({
      addNotification: vi.fn(),
    } as any);

    vi.mocked(useUserAccountModule.useActivateUserAccount).mockReturnValue({
      mutate: vi.fn(),
    } as any);

    vi.mocked(useUserAccountModule.useBlockUserAccount).mockReturnValue({
      mutate: vi.fn(),
    } as any);

    vi.mocked(useUserAccountModule.useRestoreUserAccount).mockReturnValue({
      mutate: vi.fn(),
    } as any);
  });

  it('auto-selects first account when data loads and no selection exists', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });
    expect(result.current.selectedId).toBe('u-1');
  });

  it('returns viewMode as list by default', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });
    expect(result.current.viewMode).toBe('list');
  });

  it('returns isCreateOpen as false by default', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });
    expect(result.current.isCreateOpen).toBe(false);
  });

  it('returns showBlockDialog as false by default', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });
    expect(result.current.showBlockDialog).toBe(false);
  });

  it('returns showRestoreDialog as false by default', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });
    expect(result.current.showRestoreDialog).toBe(false);
  });

  it('returns knownAccounts from local overrides', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });
    expect(result.current.knownAccounts).toEqual(mockAccounts);
  });

  it('returns tenants from tenant query', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });
    expect(result.current.tenants).toEqual(mockTenants);
  });

  it('auto-selects first tenant when tenants load', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });
    expect(result.current.selectedTenantId).toBe('t-1');
  });

  it('handleSelectAccount sets selectedId', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });

    act(() => {
      result.current.handleSelectAccount('u-2');
    });

    expect(result.current.selectedId).toBe('u-2');
  });

  it('handleBlockRequest sets selectedId and shows block dialog', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });

    act(() => {
      result.current.handleBlockRequest('u-1');
    });

    expect(result.current.selectedId).toBe('u-1');
    expect(result.current.showBlockDialog).toBe(true);
  });

  it('handleActivate calls mutate on activateMutation', () => {
    const mutate = vi.fn();
    vi.mocked(useUserAccountModule.useActivateUserAccount).mockReturnValue({
      mutate,
    } as any);

    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });

    act(() => {
      result.current.handleActivate();
    });

    expect(mutate).toHaveBeenCalled();
  });

  it('confirmBlock calls mutate with block reason', () => {
    const addNotification = vi.fn();
    const mutate = vi.fn();
    vi.mocked(notificationStoreModule.useNotificationStore).mockReturnValue({
      addNotification,
    } as any);
    vi.mocked(useUserAccountModule.useBlockUserAccount).mockReturnValue({
      mutate,
    } as any);

    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });

    act(() => {
      result.current.setBlockReason('Policy violation');
    });

    act(() => {
      result.current.confirmBlock();
    });

    expect(mutate).toHaveBeenCalledWith('Policy violation', expect.any(Object));
  });

  it('confirmBlock does nothing with empty reason', () => {
    const mutate = vi.fn();
    vi.mocked(useUserAccountModule.useBlockUserAccount).mockReturnValue({
      mutate,
    } as any);

    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });

    act(() => {
      result.current.confirmBlock();
    });

    expect(mutate).not.toHaveBeenCalled();
  });

  it('confirmRestore calls mutate on restoreMutation', () => {
    const addNotification = vi.fn();
    const mutate = vi.fn();
    vi.mocked(notificationStoreModule.useNotificationStore).mockReturnValue({
      addNotification,
    } as any);
    vi.mocked(useUserAccountModule.useRestoreUserAccount).mockReturnValue({
      mutate,
    } as any);

    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });

    act(() => {
      result.current.setSelectedId('u-2');
    });

    act(() => {
      result.current.showRestoreDialog = true;
    });

    act(() => {
      result.current.confirmRestore();
    });

    expect(mutate).toHaveBeenCalled();
  });

  it('handleCreateSuccess resets pagination and query state', () => {
    const setPage = vi.fn();
    const handleResetQuery = vi.fn();

    vi.mocked(usePaginationStateModule.usePaginationState).mockReturnValue({
      page: 1,
      setPage,
      pageSize: 20,
      setPageSize: vi.fn(),
      startIndex: 0,
      handlePageChange: vi.fn(),
      handlePageSizeChange: vi.fn(),
    } as any);

    vi.mocked(useQueryStateModule.useQueryState).mockReturnValue({
      searchCriteria: 'email',
      setSearchCriteria: vi.fn(),
      searchValue: '',
      setSearchValue: vi.fn(),
      activeFilter: 'all',
      setActiveFilter: vi.fn(),
      sortBy: 'email',
      setSortBy: vi.fn(),
      sortOrder: 'asc',
      setSortOrder: vi.fn(),
      toggleSortOrder: vi.fn(),
      appliedQuery: { criteria: 'email', term: '' },
      handleQuerySubmit: vi.fn(),
      handleResetQuery,
    } as any);

    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });

    act(() => {
      result.current.handleCreateSuccess();
    });

    expect(setPage).toHaveBeenCalledWith(1);
    expect(handleResetQuery).toHaveBeenCalled();
  });

  it('patchAccount calls patchItem from local overrides', () => {
    const patchItem = vi.fn();
    vi.mocked(useLocalOverridesModule.useLocalOverrides).mockReturnValue({
      items: mockAccounts,
      patchItem,
      patchItems: vi.fn(),
      clearOverrides: vi.fn(),
      rollbackItem: vi.fn(),
      rollbackAll: vi.fn(),
      getDiffs: vi.fn(() => []),
      isDirty: vi.fn(() => false),
    });

    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });

    act(() => {
      result.current.patchAccount('u-1', { category: 'B2B' });
    });

    expect(patchItem).toHaveBeenCalledWith('u-1', { category: 'B2B' });
  });

  it('setViewMode updates viewMode', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });

    act(() => {
      result.current.setViewMode('thumbnail');
    });

    expect(result.current.viewMode).toBe('thumbnail');
  });

  it('setIsCreateOpen updates isCreateOpen', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });

    act(() => {
      result.current.setIsCreateOpen(true);
    });

    expect(result.current.isCreateOpen).toBe(true);
  });

  it('returns activeAccount matching selectedId', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });

    act(() => {
      result.current.setSelectedId('u-1');
    });

    expect(result.current.activeAccount).toEqual(mockAccounts[0]);
  });

  it('returns totalItems from page data', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });
    expect(result.current.totalItems).toBe(2);
  });

  it('returns totalPages from page data', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });
    expect(result.current.totalPages).toBe(1);
  });

  it('returns isLoadingList from query', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });
    expect(result.current.isLoadingList).toBe(false);
  });

  it('returns listError from query', () => {
    const { result } = renderHook(() => useUserAccountDashboard(), { wrapper: createWrapper() });
    expect(result.current.listError).toBeNull();
  });
});
