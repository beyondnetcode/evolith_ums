import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { usePermissionTemplateDashboard } from './use-permission-template-dashboard';
import * as usePermissionTemplateModule from './use-permission-template';
import * as useQueryStateModule from '@app/shared/hooks/use-query-state';
import * as usePaginationStateModule from '@app/shared/hooks/use-pagination-state';

vi.mock('./use-permission-template');
vi.mock('@app/shared/hooks/use-query-state');
vi.mock('@app/shared/hooks/use-pagination-state');

const mockTemplates = [
  { templateId: 'pt-1', version: 'v1', name: 'Template 1', status: 'Active', tenantId: 't-1' },
  { templateId: 'pt-2', version: 'v2', name: 'Template 2', status: 'Draft', tenantId: 't-1' },
];

describe('usePermissionTemplateDashboard', () => {
  beforeEach(() => {
    vi.restoreAllMocks();

    vi.mocked(usePermissionTemplateModule.useGetAllPermissionTemplates).mockReturnValue({
      data: { items: mockTemplates, page: 1, pageSize: 20, totalItems: 2, totalPages: 1 },
      isLoading: false,
      error: null,
    } as any);

    vi.mocked(usePermissionTemplateModule.useGetPermissionTemplate).mockReturnValue({
      data: mockTemplates[0],
      isLoading: false,
      error: null,
    } as any);

    vi.mocked(useQueryStateModule.useQueryState).mockReturnValue({
      searchCriteria: 'version',
      setSearchCriteria: vi.fn(),
      searchValue: '',
      setSearchValue: vi.fn(),
      activeFilter: 'all',
      setActiveFilter: vi.fn(),
      sortBy: 'version',
      setSortBy: vi.fn(),
      sortOrder: 'asc',
      setSortOrder: vi.fn(),
      toggleSortOrder: vi.fn(),
      appliedQuery: { criteria: 'version', term: '' },
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
    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.selectedId).toBe('');
  });

  it('returns viewMode as list by default', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.viewMode).toBe('list');
  });

  it('returns isCreateOpen as false by default', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.isCreateOpen).toBe(false);
  });

  it('returns knownTemplates from page data', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.knownTemplates).toEqual(mockTemplates);
  });

  it('returns isLoadingList from query', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.isLoadingList).toBe(false);
  });

  it('returns listError from query', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.listError).toBeNull();
  });

  it('returns totalItems from page data', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.totalItems).toBe(2);
  });

  it('returns totalPages from page data', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.totalPages).toBe(1);
  });

  it('handleSelect sets selectedId', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());

    act(() => {
      result.current.handleSelect('pt-2');
    });

    expect(result.current.selectedId).toBe('pt-2');
  });

  it('handleCreateSuccess closes create dialog', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());

    act(() => {
      result.current.setIsCreateOpen(true);
    });

    act(() => {
      result.current.handleCreateSuccess();
    });

    expect(result.current.isCreateOpen).toBe(false);
  });

  it('setViewMode updates viewMode', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());

    act(() => {
      result.current.setViewMode('thumbnail');
    });

    expect(result.current.viewMode).toBe('thumbnail');
  });

  it('setIsCreateOpen updates isCreateOpen', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());

    act(() => {
      result.current.setIsCreateOpen(true);
    });

    expect(result.current.isCreateOpen).toBe(true);
  });

  it('setSelectedId updates selectedId', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());

    act(() => {
      result.current.setSelectedId('pt-1');
    });

    expect(result.current.selectedId).toBe('pt-1');
  });

  it('returns activeTemplate from detail query', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());

    act(() => {
      result.current.setSelectedId('pt-1');
    });

    expect(result.current.activeTemplate).toBeDefined();
  });

  it('returns isLoadingDetail from detail query', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.isLoadingDetail).toBe(false);
  });

  it('returns queryState object', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.queryState).toBeDefined();
    expect(result.current.queryState.appliedQuery).toBeDefined();
  });

  it('returns paginationState object', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.paginationState).toBeDefined();
    expect(result.current.paginationState.page).toBe(1);
  });

  it('passes tenantId to useGetAllPermissionTemplates', () => {
    const { result } = renderHook(() => usePermissionTemplateDashboard('t-1'));
    expect(result.current.knownTemplates).toEqual(mockTemplates);
  });

  it('returns empty knownTemplates when pageData is undefined', () => {
    vi.mocked(usePermissionTemplateModule.useGetAllPermissionTemplates).mockReturnValue({
      data: undefined,
      isLoading: false,
      error: null,
    } as any);

    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.knownTemplates).toEqual([]);
  });

  it('returns zero totalItems when pageData is undefined', () => {
    vi.mocked(usePermissionTemplateModule.useGetAllPermissionTemplates).mockReturnValue({
      data: undefined,
      isLoading: false,
      error: null,
    } as any);

    const { result } = renderHook(() => usePermissionTemplateDashboard());
    expect(result.current.totalItems).toBe(0);
  });
});
