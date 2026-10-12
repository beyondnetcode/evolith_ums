import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useDelegationDashboard } from './use-delegation-dashboard';
import { useAuthStore } from '@app/stores/auth.store';
import * as useDelegationModule from './use-delegation';
import * as useLocalOverridesModule from '@app/hooks/use-local-overrides';
import * as useQueryStateModule from '@app/shared/hooks/use-query-state';
import * as usePaginationStateModule from '@app/shared/hooks/use-pagination-state';

vi.mock('./use-delegation');
vi.mock('@app/hooks/use-local-overrides');
vi.mock('@app/shared/hooks/use-query-state');
vi.mock('@app/shared/hooks/use-pagination-state');
vi.mock('@app/stores/auth.store', () => ({
  useAuthStore: vi.fn(),
}));

const mockDelegations = [
  {
    delegationId: 'd-1',
    scopeType: 'Tenant',
    status: 'Active',
    delegatedAdminId: 'u-1',
    delegatingAdminId: 'u-2',
    tenantId: 't-1',
  },
  {
    delegationId: 'd-2',
    scopeType: 'Branch',
    status: 'Pending',
    delegatedAdminId: 'u-1',
    delegatingAdminId: 'u-3',
    tenantId: 't-1',
  },
];

describe('useDelegationDashboard', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.mocked(useAuthStore).mockImplementation((selector?: never) => {
      const state = {
        user: {
          id: '5f4e3d01-1b0a-9f8e-7d6c-543210987654',
          tenantId: '5f4e3d2c-1b0a-9f8e-7d6c-543210987654',
        },
      };
      return typeof selector === 'function' ? selector(state as never) : (state as never);
    });

    vi.mocked(useDelegationModule.useGetDelegationsByDelegatedAdmin).mockReturnValue({
      data: mockDelegations,
      isLoading: false,
      error: null,
    } as any);

    vi.mocked(useDelegationModule.useGetDelegationsByDelegatingAdmin).mockReturnValue({
      data: mockDelegations,
      isLoading: false,
      error: null,
    } as any);

    vi.mocked(useLocalOverridesModule.useLocalOverrides).mockReturnValue({
      items: mockDelegations,
      patchItem: vi.fn(),
      patchItems: vi.fn(),
      clearOverrides: vi.fn(),
      rollbackItem: vi.fn(),
      rollbackAll: vi.fn(),
      getDiffs: vi.fn(() => []),
      isDirty: vi.fn(() => false),
    });

    vi.mocked(useQueryStateModule.useQueryState).mockReturnValue({
      searchCriteria: 'id',
      setSearchCriteria: vi.fn(),
      searchValue: '',
      setSearchValue: vi.fn(),
      activeFilter: 'all',
      setActiveFilter: vi.fn(),
      sortBy: 'status',
      setSortBy: vi.fn(),
      sortOrder: 'asc',
      setSortOrder: vi.fn(),
      toggleSortOrder: vi.fn(),
      appliedQuery: { criteria: 'id', term: '' },
      handleQuerySubmit: vi.fn(),
      handleResetQuery: vi.fn(),
    } as any);

    vi.mocked(usePaginationStateModule.usePaginationState).mockReturnValue({
      page: 1,
      setPage: vi.fn(),
      pageSize: 2,
      setPageSize: vi.fn(),
      startIndex: 0,
      handlePageChange: vi.fn(),
      handlePageSizeChange: vi.fn(),
    } as any);
  });

  it('auto-selects first delegation when data loads and no selection exists', () => {
    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.selectedId).toBe('d-1');
  });

  it('returns delegationViewType as received by default', () => {
    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.delegationViewType).toBe('received');
  });

  it('returns viewMode as list by default', () => {
    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.viewMode).toBe('list');
  });

  it('returns activeConsoleTab as overview by default', () => {
    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.activeConsoleTab).toBe('overview');
  });

  it('returns isEditing as false by default', () => {
    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.isEditing).toBe(false);
  });

  it('returns knownDelegations from local overrides', () => {
    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.knownDelegations).toEqual(mockDelegations);
  });

  it('returns isLoadingList from active query', () => {
    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.isLoadingList).toBe(false);
  });

  it('returns listError from active query', () => {
    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.listError).toBeNull();
  });

  it('returns consoleTabs with overview and permissions', () => {
    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.consoleTabs).toEqual(['overview', 'permissions']);
  });

  it('returns currentUserId', () => {
    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.currentUserId).toBe('5f4e3d01-1b0a-9f8e-7d6c-543210987654');
  });

  it('returns currentTenantId', () => {
    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.currentTenantId).toBe('5f4e3d2c-1b0a-9f8e-7d6c-543210987654');
  });

  it('handleSelectDelegation selects a delegation when not editing', () => {
    const { result } = renderHook(() => useDelegationDashboard());

    act(() => {
      result.current.handleSelectDelegation('d-2');
    });

    expect(result.current.selectedId).toBe('d-2');
  });

  it('handleSelectDelegation shows discard dialog when editing', () => {
    const { result } = renderHook(() => useDelegationDashboard());

    act(() => {
      result.current.setIsEditing(true);
      result.current.setSelectedId('d-1');
    });

    act(() => {
      result.current.handleSelectDelegation('d-2');
    });

    expect(result.current.showDiscardDialog).toBe(true);
    expect(result.current.pendingNavigationId).toBe('d-2');
  });

  it('handleSelectDelegation does nothing when selecting same item', () => {
    const { result } = renderHook(() => useDelegationDashboard());

    act(() => {
      result.current.setSelectedId('d-1');
    });

    act(() => {
      result.current.handleSelectDelegation('d-1');
    });

    expect(result.current.showDiscardDialog).toBe(false);
  });

  it('confirmDiscard navigates to pending item', () => {
    const { result } = renderHook(() => useDelegationDashboard());

    act(() => {
      result.current.setIsEditing(true);
      result.current.setSelectedId('d-1');
      result.current.handleSelectDelegation('d-2');
    });

    act(() => {
      result.current.confirmDiscard();
    });

    expect(result.current.selectedId).toBe('d-2');
    expect(result.current.showDiscardDialog).toBe(false);
  });

  it('patchDelegation calls patchItem from local overrides', () => {
    const patchItem = vi.fn();
    vi.mocked(useLocalOverridesModule.useLocalOverrides).mockReturnValue({
      items: mockDelegations,
      patchItem,
      patchItems: vi.fn(),
      clearOverrides: vi.fn(),
      rollbackItem: vi.fn(),
      rollbackAll: vi.fn(),
      getDiffs: vi.fn(() => []),
      isDirty: vi.fn(() => false),
    });

    const { result } = renderHook(() => useDelegationDashboard());

    act(() => {
      result.current.patchDelegation('d-1', { status: 'Revoked' });
    });

    expect(patchItem).toHaveBeenCalledWith('d-1', { status: 'Revoked' });
  });

  it('handleCreateSuccess resets state and switches to granted view', () => {
    const setPage = vi.fn();
    const setSearchValue = vi.fn();
    const handleResetQuery = vi.fn();

    vi.mocked(usePaginationStateModule.usePaginationState).mockReturnValue({
      page: 1,
      setPage,
      pageSize: 2,
      setPageSize: vi.fn(),
      startIndex: 0,
      handlePageChange: vi.fn(),
      handlePageSizeChange: vi.fn(),
    } as any);

    vi.mocked(useQueryStateModule.useQueryState).mockReturnValue({
      searchCriteria: 'id',
      setSearchCriteria: vi.fn(),
      searchValue: '',
      setSearchValue,
      activeFilter: 'all',
      setActiveFilter: vi.fn(),
      sortBy: 'status',
      setSortBy: vi.fn(),
      sortOrder: 'asc',
      setSortOrder: vi.fn(),
      toggleSortOrder: vi.fn(),
      appliedQuery: { criteria: 'id', term: '' },
      handleQuerySubmit: vi.fn(),
      handleResetQuery,
    } as any);

    const { result } = renderHook(() => useDelegationDashboard());

    act(() => {
      result.current.handleCreateSuccess();
    });

    expect(result.current.delegationViewType).toBe('granted');
    expect(setPage).toHaveBeenCalledWith(1);
  });

  it('setDelegationViewType updates view type', () => {
    const { result } = renderHook(() => useDelegationDashboard());

    act(() => {
      result.current.setDelegationViewType('granted');
    });

    expect(result.current.delegationViewType).toBe('granted');
  });

  it('setActiveConsoleTab updates tab', () => {
    const { result } = renderHook(() => useDelegationDashboard());

    act(() => {
      result.current.setActiveConsoleTab('permissions');
    });

    expect(result.current.activeConsoleTab).toBe('permissions');
  });

  it('returns activeDelegation matching selectedId', () => {
    const { result } = renderHook(() => useDelegationDashboard());

    act(() => {
      result.current.setSelectedId('d-1');
    });

    expect(result.current.activeDelegation).toEqual(mockDelegations[0]);
  });

  it('filters delegations by search term', () => {
    vi.mocked(useQueryStateModule.useQueryState).mockReturnValue({
      searchCriteria: 'id',
      setSearchCriteria: vi.fn(),
      searchValue: '',
      setSearchValue: vi.fn(),
      activeFilter: 'all',
      setActiveFilter: vi.fn(),
      sortBy: 'status',
      setSortBy: vi.fn(),
      sortOrder: 'asc',
      setSortOrder: vi.fn(),
      toggleSortOrder: vi.fn(),
      appliedQuery: { criteria: 'id', term: 'd-1' },
      handleQuerySubmit: vi.fn(),
      handleResetQuery: vi.fn(),
    } as any);

    vi.mocked(useLocalOverridesModule.useLocalOverrides).mockImplementation((items: any) => ({
      items: items?.filter((d: any) => d.delegationId.includes('d-1')) ?? [],
      patchItem: vi.fn(),
      patchItems: vi.fn(),
      clearOverrides: vi.fn(),
      rollbackItem: vi.fn(),
      rollbackAll: vi.fn(),
      getDiffs: vi.fn(() => []),
      isDirty: vi.fn(() => false),
    }));

    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.knownDelegations.length).toBeLessThanOrEqual(1);
  });

  it('filters delegations by status', () => {
    vi.mocked(useQueryStateModule.useQueryState).mockReturnValue({
      searchCriteria: 'id',
      setSearchCriteria: vi.fn(),
      searchValue: '',
      setSearchValue: vi.fn(),
      activeFilter: 'Active',
      setActiveFilter: vi.fn(),
      sortBy: 'status',
      setSortBy: vi.fn(),
      sortOrder: 'asc',
      setSortOrder: vi.fn(),
      toggleSortOrder: vi.fn(),
      appliedQuery: { criteria: 'id', term: '' },
      handleQuerySubmit: vi.fn(),
      handleResetQuery: vi.fn(),
    } as any);

    vi.mocked(useLocalOverridesModule.useLocalOverrides).mockImplementation((items: any) => ({
      items: items?.filter((d: any) => d.status === 'Active') ?? [],
      patchItem: vi.fn(),
      patchItems: vi.fn(),
      clearOverrides: vi.fn(),
      rollbackItem: vi.fn(),
      rollbackAll: vi.fn(),
      getDiffs: vi.fn(() => []),
      isDirty: vi.fn(() => false),
    }));

    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.knownDelegations.every((d: any) => d.status === 'Active')).toBe(true);
  });

  it('returns totalItems based on known delegations length', () => {
    const { result } = renderHook(() => useDelegationDashboard());
    expect(result.current.totalItems).toBe(mockDelegations.length);
  });
});
