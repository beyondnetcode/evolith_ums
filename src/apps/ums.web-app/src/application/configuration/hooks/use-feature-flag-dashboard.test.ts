import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useFeatureFlagDashboard } from './use-feature-flag-dashboard';
import * as useFeatureFlagModule from '@app/configuration/hooks/use-feature-flag';
import * as useQueryStateModule from '@app/shared/hooks/use-query-state';
import * as usePaginationStateModule from '@app/shared/hooks/use-pagination-state';

vi.mock('@app/configuration/hooks/use-feature-flag');
vi.mock('@app/shared/hooks/use-query-state');
vi.mock('@app/shared/hooks/use-pagination-state');

const mockFlags = [
  {
    flagId: 'f-1',
    flagCode: 'FLAG_1',
    name: 'Flag 1',
    flagType: 'Boolean',
    status: 'Active',
    tenantId: 't-1',
  },
  {
    flagId: 'f-2',
    flagCode: 'FLAG_2',
    name: 'Flag 2',
    flagType: 'Variant',
    status: 'Inactive',
    tenantId: 't-1',
  },
];

describe('useFeatureFlagDashboard', () => {
  beforeEach(() => {
    vi.restoreAllMocks();

    vi.mocked(useFeatureFlagModule.useGetAllFeatureFlags).mockReturnValue({
      data: { items: mockFlags, page: 1, pageSize: 20, totalItems: 2, totalPages: 1 },
      isLoading: false,
      error: null,
    } as any);

    vi.mocked(useFeatureFlagModule.useGetFeatureFlagById).mockReturnValue({
      data: mockFlags[0],
      isLoading: false,
      error: null,
    } as any);

    vi.mocked(useQueryStateModule.useQueryState).mockReturnValue({
      searchCriteria: 'flagCode',
      setSearchCriteria: vi.fn(),
      searchValue: '',
      setSearchValue: vi.fn(),
      activeFilter: 'all',
      setActiveFilter: vi.fn(),
      sortBy: 'flagCode',
      setSortBy: vi.fn(),
      sortOrder: 'asc',
      setSortOrder: vi.fn(),
      toggleSortOrder: vi.fn(),
      appliedQuery: { criteria: 'flagCode', term: '' },
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

  it('returns initial state with empty selectedId', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.selectedId).toBe('');
  });

  it('returns viewMode as list by default', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.viewMode).toBe('list');
  });

  it('returns isCreateOpen as false by default', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.isCreateOpen).toBe(false);
  });

  it('returns knownFlags from page data', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.knownFlags).toEqual(mockFlags);
  });

  it('returns isLoadingList from query', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.isLoadingList).toBe(false);
  });

  it('returns listError from query', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.listError).toBeNull();
  });

  it('returns totalItems from page data', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.totalItems).toBe(2);
  });

  it('returns totalPages from page data', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.totalPages).toBe(1);
  });

  it('handleSelect sets selectedId', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());

    act(() => {
      result.current.handleSelect('f-2');
    });

    expect(result.current.selectedId).toBe('f-2');
  });

  it('handleCreateSuccess closes create dialog', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());

    act(() => {
      result.current.setIsCreateOpen(true);
    });

    act(() => {
      result.current.handleCreateSuccess();
    });

    expect(result.current.isCreateOpen).toBe(false);
  });

  it('setViewMode updates viewMode', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());

    act(() => {
      result.current.setViewMode('thumbnail');
    });

    expect(result.current.viewMode).toBe('thumbnail');
  });

  it('setIsCreateOpen updates isCreateOpen', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());

    act(() => {
      result.current.setIsCreateOpen(true);
    });

    expect(result.current.isCreateOpen).toBe(true);
  });

  it('setSelectedId updates selectedId', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());

    act(() => {
      result.current.setSelectedId('f-1');
    });

    expect(result.current.selectedId).toBe('f-1');
  });

  it('returns activeFlag from detail query', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());

    act(() => {
      result.current.setSelectedId('f-1');
    });

    expect(result.current.activeFlag).toBeDefined();
  });

  it('returns isLoadingDetail from detail query', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.isLoadingDetail).toBe(false);
  });

  it('returns queryState object', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.queryState).toBeDefined();
    expect(result.current.queryState.appliedQuery).toBeDefined();
  });

  it('returns paginationState object', () => {
    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.paginationState).toBeDefined();
    expect(result.current.paginationState.page).toBe(1);
  });

  it('returns empty knownFlags when pageData is undefined', () => {
    vi.mocked(useFeatureFlagModule.useGetAllFeatureFlags).mockReturnValue({
      data: undefined,
      isLoading: false,
      error: null,
    } as any);

    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.knownFlags).toEqual([]);
  });

  it('returns zero totalItems when pageData is undefined', () => {
    vi.mocked(useFeatureFlagModule.useGetAllFeatureFlags).mockReturnValue({
      data: undefined,
      isLoading: false,
      error: null,
    } as any);

    const { result } = renderHook(() => useFeatureFlagDashboard());
    expect(result.current.totalItems).toBe(0);
  });
});
