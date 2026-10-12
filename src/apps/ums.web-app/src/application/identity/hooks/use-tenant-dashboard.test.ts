import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useTenantDashboard } from './use-tenant-dashboard';
import * as useTenantModule from '@app/identity/hooks/use-tenant';
import * as useLocalOverridesModule from '@app/hooks/use-local-overrides';
import * as useQueryStateModule from '@app/shared/hooks/use-query-state';
import * as usePaginationStateModule from '@app/shared/hooks/use-pagination-state';

vi.mock('@app/identity/hooks/use-tenant');
vi.mock('@app/hooks/use-local-overrides');
vi.mock('@app/shared/hooks/use-query-state');
vi.mock('@app/shared/hooks/use-pagination-state');
vi.mock('@domain/identity/constants/tenant.constants', () => ({
  TENANT_PAGE_SIZE: 9,
}));

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
  {
    tenantId: 't-2',
    code: 'T2',
    name: 'Tenant 2',
    type: 'CLIENT',
    status: 'Active',
    parentTenantId: 't-1',
    companyReference: null,
  },
];

describe('useTenantDashboard', () => {
  beforeEach(() => {
    vi.restoreAllMocks();

    vi.mocked(useTenantModule.useGetAllTenants).mockReturnValue({
      data: { items: mockTenants, page: 1, pageSize: 9, totalItems: 2, totalPages: 1 },
      isLoading: false,
      error: null,
    } as any);

    vi.mocked(useLocalOverridesModule.useLocalOverrides).mockReturnValue({
      items: mockTenants,
      patchItem: vi.fn(),
      patchItems: vi.fn(),
      clearOverrides: vi.fn(),
      rollbackItem: vi.fn(),
      rollbackAll: vi.fn(),
      getDiffs: vi.fn(() => []),
      isDirty: vi.fn(() => false),
    });

    vi.mocked(useQueryStateModule.useQueryState).mockReturnValue({
      searchCriteria: 'name',
      setSearchCriteria: vi.fn(),
      searchValue: '',
      setSearchValue: vi.fn(),
      activeFilter: 'all',
      setActiveFilter: vi.fn(),
      sortBy: 'name',
      setSortBy: vi.fn(),
      sortOrder: 'asc',
      setSortOrder: vi.fn(),
      toggleSortOrder: vi.fn(),
      appliedQuery: { criteria: 'name', term: '' },
      handleQuerySubmit: vi.fn(),
      handleResetQuery: vi.fn(),
    } as any);

    vi.mocked(usePaginationStateModule.usePaginationState).mockReturnValue({
      page: 1,
      setPage: vi.fn(),
      pageSize: 9,
      setPageSize: vi.fn(),
      startIndex: 0,
      handlePageChange: vi.fn(),
      handlePageSizeChange: vi.fn(),
    } as any);
  });

  it('auto-selects root tenant when data loads and no selection exists', () => {
    const { result } = renderHook(() => useTenantDashboard());
    expect(result.current.selectedId).toBe('t-1');
  });

  it('returns viewMode as list by default', () => {
    const { result } = renderHook(() => useTenantDashboard());
    expect(result.current.viewMode).toBe('list');
  });

  it('returns activeConsoleTab as branches by default', () => {
    const { result } = renderHook(() => useTenantDashboard());
    expect(result.current.activeConsoleTab).toBe('branches');
  });

  it('returns isTenantEditing as false by default', () => {
    const { result } = renderHook(() => useTenantDashboard());
    expect(result.current.isTenantEditing).toBe(false);
  });

  it('returns knownTenants from local overrides', () => {
    const { result } = renderHook(() => useTenantDashboard());
    expect(result.current.knownTenants).toEqual(mockTenants);
  });

  it('returns isLoadingList from query', () => {
    const { result } = renderHook(() => useTenantDashboard());
    expect(result.current.isLoadingList).toBe(false);
  });

  it('returns isRootTenant as true for tenant without parent', () => {
    const { result } = renderHook(() => useTenantDashboard());

    act(() => {
      result.current.setSelectedId('t-1');
    });

    expect(result.current.isRootTenant).toBe(true);
  });

  it('returns isRootTenant as false for tenant with parent', () => {
    const { result } = renderHook(() => useTenantDashboard());

    act(() => {
      result.current.setSelectedId('t-2');
    });

    expect(result.current.isRootTenant).toBe(false);
  });

  it('returns parentTenant for child tenant', () => {
    const { result } = renderHook(() => useTenantDashboard());

    act(() => {
      result.current.setSelectedId('t-2');
    });

    expect(result.current.parentTenant).toEqual(mockTenants[0]);
  });

  it('returns null parentTenant for root tenant', () => {
    const { result } = renderHook(() => useTenantDashboard());

    act(() => {
      result.current.setSelectedId('t-1');
    });

    expect(result.current.parentTenant).toBeNull();
  });

  it('handleSelectTenant selects a tenant when not editing', () => {
    const { result } = renderHook(() => useTenantDashboard());

    act(() => {
      result.current.handleSelectTenant('t-2');
    });

    expect(result.current.selectedId).toBe('t-2');
  });

  it('handleSelectTenant shows discard dialog when editing', () => {
    const { result } = renderHook(() => useTenantDashboard());

    act(() => {
      result.current.setIsTenantEditing(true);
      result.current.setSelectedId('t-1');
    });

    act(() => {
      result.current.handleSelectTenant('t-2');
    });

    expect(result.current.showDiscardDialog).toBe(true);
    expect(result.current.pendingNavigationId).toBe('t-2');
  });

  it('confirmDiscard navigates to pending item', () => {
    const { result } = renderHook(() => useTenantDashboard());

    act(() => {
      result.current.setIsTenantEditing(true);
      result.current.setSelectedId('t-1');
      result.current.handleSelectTenant('t-2');
    });

    act(() => {
      result.current.confirmDiscard();
    });

    expect(result.current.selectedId).toBe('t-2');
    expect(result.current.showDiscardDialog).toBe(false);
  });

  it('patchTenant calls patchItem from local overrides', () => {
    const patchItem = vi.fn();
    vi.mocked(useLocalOverridesModule.useLocalOverrides).mockReturnValue({
      items: mockTenants,
      patchItem,
      patchItems: vi.fn(),
      clearOverrides: vi.fn(),
      rollbackItem: vi.fn(),
      rollbackAll: vi.fn(),
      getDiffs: vi.fn(() => []),
      isDirty: vi.fn(() => false),
    });

    const { result } = renderHook(() => useTenantDashboard());

    act(() => {
      result.current.patchTenant('t-1', { name: 'Updated' });
    });

    expect(patchItem).toHaveBeenCalledWith('t-1', { name: 'Updated' });
  });

  it('handleCreateSuccess resets state and selects new tenant', () => {
    const setPage = vi.fn();
    const handleResetQuery = vi.fn();

    vi.mocked(usePaginationStateModule.usePaginationState).mockReturnValue({
      page: 1,
      setPage,
      pageSize: 9,
      setPageSize: vi.fn(),
      startIndex: 0,
      handlePageChange: vi.fn(),
      handlePageSizeChange: vi.fn(),
    } as any);

    vi.mocked(useQueryStateModule.useQueryState).mockReturnValue({
      searchCriteria: 'name',
      setSearchCriteria: vi.fn(),
      searchValue: '',
      setSearchValue: vi.fn(),
      activeFilter: 'all',
      setActiveFilter: vi.fn(),
      sortBy: 'name',
      setSortBy: vi.fn(),
      sortOrder: 'asc',
      setSortOrder: vi.fn(),
      toggleSortOrder: vi.fn(),
      appliedQuery: { criteria: 'name', term: '' },
      handleQuerySubmit: vi.fn(),
      handleResetQuery,
    } as any);

    const { result } = renderHook(() => useTenantDashboard());

    act(() => {
      result.current.handleCreateSuccess('t-new');
    });

    expect(setPage).toHaveBeenCalledWith(1);
    expect(handleResetQuery).toHaveBeenCalled();
  });

  it('setActiveConsoleTab updates tab', () => {
    const { result } = renderHook(() => useTenantDashboard());

    act(() => {
      result.current.setActiveConsoleTab('providers');
    });

    expect(result.current.activeConsoleTab).toBe('providers');
  });

  it('setViewMode updates viewMode', () => {
    const { result } = renderHook(() => useTenantDashboard());

    act(() => {
      result.current.setViewMode('thumbnail');
    });

    expect(result.current.viewMode).toBe('thumbnail');
  });

  it('returns totalItems from page data', () => {
    const { result } = renderHook(() => useTenantDashboard());
    expect(result.current.totalItems).toBe(2);
  });

  it('returns activeTenant matching selectedId', () => {
    const { result } = renderHook(() => useTenantDashboard());

    act(() => {
      result.current.setSelectedId('t-1');
    });

    expect(result.current.activeTenant).toEqual(mockTenants[0]);
  });

  it('triggers handleSelectTenant when query criteria is id', () => {
    const appliedQuery = { criteria: 'id', term: 't-2' };
    vi.mocked(useQueryStateModule.useQueryState).mockReturnValue({
      searchCriteria: 'id',
      setSearchCriteria: vi.fn(),
      searchValue: 't-2',
      setSearchValue: vi.fn(),
      activeFilter: 'all',
      setActiveFilter: vi.fn(),
      sortBy: 'name',
      setSortBy: vi.fn(),
      sortOrder: 'asc',
      setSortOrder: vi.fn(),
      toggleSortOrder: vi.fn(),
      appliedQuery,
      handleQuerySubmit: vi.fn(),
      handleResetQuery: vi.fn(),
    } as any);

    const { result } = renderHook(() => useTenantDashboard());

    expect(result.current.selectedId).toBe('t-2');
  });
});
