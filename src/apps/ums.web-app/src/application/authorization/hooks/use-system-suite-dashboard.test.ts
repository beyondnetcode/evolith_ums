import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useSystemSuiteDashboard } from './use-system-suite-dashboard';
import * as useSystemSuiteModule from '@app/authorization/hooks/use-system-suite';
import * as useLocalOverridesModule from '@app/hooks/use-local-overrides';
import * as useQueryStateModule from '@app/shared/hooks/use-query-state';
import * as usePaginationStateModule from '@app/shared/hooks/use-pagination-state';

vi.mock('@app/authorization/hooks/use-system-suite');
vi.mock('@app/hooks/use-local-overrides');
vi.mock('@app/shared/hooks/use-query-state');
vi.mock('@app/shared/hooks/use-pagination-state');
vi.mock('@domain/authorization/constants/system-suite.constants', () => ({
  SYSTEM_SUITE_PAGE_SIZE: 20,
}));

const mockSystemSuites = [
  { systemSuiteId: 'ss-1', code: 'SS1', name: 'Suite 1', status: 'Active', tenantId: 't-1' },
  { systemSuiteId: 'ss-2', code: 'SS2', name: 'Suite 2', status: 'Active', tenantId: 't-1' },
];

describe('useSystemSuiteDashboard', () => {
  beforeEach(() => {
    vi.restoreAllMocks();

    vi.mocked(useSystemSuiteModule.useGetAllSystemSuites).mockReturnValue({
      data: { items: mockSystemSuites, page: 1, pageSize: 20, totalItems: 2, totalPages: 1 },
      isLoading: false,
      error: null,
    } as any);

    vi.mocked(useLocalOverridesModule.useLocalOverrides).mockReturnValue({
      items: mockSystemSuites,
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
      pageSize: 20,
      setPageSize: vi.fn(),
      startIndex: 0,
      handlePageChange: vi.fn(),
      handlePageSizeChange: vi.fn(),
    } as any);
  });

  it('auto-selects first suite when data loads and no selection exists', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.selectedId).toBe('ss-1');
  });

  it('returns viewMode as list by default', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.viewMode).toBe('list');
  });

  it('returns isCreateOpen as false by default', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.isCreateOpen).toBe(false);
  });

  it('returns isEditing as false by default', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.isEditing).toBe(false);
  });

  it('returns showDiscardDialog as false by default', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.showDiscardDialog).toBe(false);
  });

  it('returns pendingNavigationId as null by default', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.pendingNavigationId).toBeNull();
  });

  it('returns knownSystemSuites from local overrides', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.knownSystemSuites).toEqual(mockSystemSuites);
  });

  it('returns isLoadingList from query', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.isLoadingList).toBe(false);
  });

  it('returns listError from query', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.listError).toBeNull();
  });

  it('returns totalItems from page data', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.totalItems).toBe(2);
  });

  it('returns totalPages from page data', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.totalPages).toBe(1);
  });

  it('auto-selects first suite when data loads and no selection exists', () => {
    const setSelectedId = vi.fn();
    vi.mocked(usePaginationStateModule.usePaginationState).mockReturnValue({
      page: 1,
      setPage: setSelectedId,
      pageSize: 20,
      setPageSize: vi.fn(),
      startIndex: 0,
      handlePageChange: vi.fn(),
      handlePageSizeChange: vi.fn(),
    } as any);

    renderHook(() => useSystemSuiteDashboard());
  });

  it('handleSelectSystemSuite selects a suite when not editing', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());

    act(() => {
      result.current.handleSelectSystemSuite('ss-2');
    });

    expect(result.current.selectedId).toBe('ss-2');
  });

  it('handleSelectSystemSuite shows discard dialog when editing', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());

    act(() => {
      result.current.setIsEditing(true);
      result.current.setSelectedId('ss-1');
    });

    act(() => {
      result.current.handleSelectSystemSuite('ss-2');
    });

    expect(result.current.showDiscardDialog).toBe(true);
    expect(result.current.pendingNavigationId).toBe('ss-2');
  });

  it('handleSelectSystemSuite does nothing when selecting same item', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());

    act(() => {
      result.current.setSelectedId('ss-1');
    });

    act(() => {
      result.current.handleSelectSystemSuite('ss-1');
    });

    expect(result.current.showDiscardDialog).toBe(false);
  });

  it('confirmDiscard navigates to pending item and closes dialog', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());

    act(() => {
      result.current.setIsEditing(true);
      result.current.setSelectedId('ss-1');
      result.current.handleSelectSystemSuite('ss-2');
    });

    act(() => {
      result.current.confirmDiscard();
    });

    expect(result.current.selectedId).toBe('ss-2');
    expect(result.current.showDiscardDialog).toBe(false);
    expect(result.current.pendingNavigationId).toBeNull();
  });

  it('patchSystemSuite calls patchItem from local overrides', () => {
    const patchItem = vi.fn();
    vi.mocked(useLocalOverridesModule.useLocalOverrides).mockReturnValue({
      items: mockSystemSuites,
      patchItem,
      patchItems: vi.fn(),
      clearOverrides: vi.fn(),
      rollbackItem: vi.fn(),
      rollbackAll: vi.fn(),
      getDiffs: vi.fn(() => []),
      isDirty: vi.fn(() => false),
    });

    const { result } = renderHook(() => useSystemSuiteDashboard());

    act(() => {
      result.current.patchSystemSuite('ss-1', { name: 'Updated' });
    });

    expect(patchItem).toHaveBeenCalledWith('ss-1', { name: 'Updated' });
  });

  it('handleCreateSuccess resets pagination and query state', () => {
    const setPage = vi.fn();
    const setSearchValue = vi.fn();
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
      searchCriteria: 'name',
      setSearchCriteria: vi.fn(),
      searchValue: '',
      setSearchValue,
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

    const { result } = renderHook(() => useSystemSuiteDashboard());

    act(() => {
      result.current.setIsCreateOpen(true);
    });

    act(() => {
      result.current.handleCreateSuccess();
    });

    expect(setPage).toHaveBeenCalledWith(1);
    expect(setSearchValue).toHaveBeenCalledWith('');
    expect(handleResetQuery).toHaveBeenCalled();
  });

  it('setViewMode updates viewMode', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());

    act(() => {
      result.current.setViewMode('thumbnail');
    });

    expect(result.current.viewMode).toBe('thumbnail');
  });

  it('setIsCreateOpen updates isCreateOpen', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());

    act(() => {
      result.current.setIsCreateOpen(true);
    });

    expect(result.current.isCreateOpen).toBe(true);
  });

  it('returns activeSystemSuite matching selectedId', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());

    act(() => {
      result.current.setSelectedId('ss-1');
    });

    expect(result.current.activeSystemSuite).toEqual(mockSystemSuites[0]);
  });

  it('returns undefined for activeSystemSuite when no match', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());

    act(() => {
      result.current.setSelectedId('nonexistent');
    });

    expect(result.current.activeSystemSuite).toBeUndefined();
  });

  it('returns queryState object', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.queryState).toBeDefined();
    expect(result.current.queryState.appliedQuery).toBeDefined();
  });

  it('returns paginationState object', () => {
    const { result } = renderHook(() => useSystemSuiteDashboard());
    expect(result.current.paginationState).toBeDefined();
    expect(result.current.paginationState.page).toBe(1);
  });
});
