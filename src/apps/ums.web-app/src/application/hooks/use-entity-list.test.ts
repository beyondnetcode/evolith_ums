import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useEntityList } from './use-entity-list';

describe('useEntityList', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('returns default values', () => {
    const { result } = renderHook(() => useEntityList());

    expect(result.current.viewMode).toBe('list');
    expect(result.current.searchCriteria).toBe('name');
    expect(result.current.searchValue).toBe('');
    expect(result.current.activeFilter).toBe('all');
    expect(result.current.sortBy).toBe('name');
    expect(result.current.sortOrder).toBe('asc');
    expect(result.current.page).toBe(1);
    expect(result.current.pageSize).toBe(10);
  });

  it('accepts custom initial values', () => {
    const { result } = renderHook(() =>
      useEntityList({
        pageSize: 25,
        initialSortBy: 'date',
        initialSortOrder: 'desc',
        initialFilter: 'active',
        initialSearchCriteria: 'email',
      })
    );

    expect(result.current.pageSize).toBe(25);
    expect(result.current.sortBy).toBe('date');
    expect(result.current.sortOrder).toBe('desc');
    expect(result.current.activeFilter).toBe('active');
    expect(result.current.searchCriteria).toBe('email');
  });

  it('resets page to 1 on query submit', () => {
    const { result } = renderHook(() => useEntityList());

    act(() => {
      result.current.setPage(5);
      result.current.setSearchValue('test');
    });

    const mockEvent = { preventDefault: vi.fn() } as unknown as React.FormEvent;
    act(() => {
      result.current.handleQuerySubmit(mockEvent);
    });

    expect(mockEvent.preventDefault).toHaveBeenCalled();
    expect(result.current.page).toBe(1);
    expect(result.current.appliedQuery.term).toBe('test');
  });

  it('resets search on handleResetQuery', () => {
    const { result } = renderHook(() => useEntityList());

    act(() => {
      result.current.setSearchValue('test');
      result.current.setPage(3);
    });
    act(() => {
      result.current.handleResetQuery();
    });

    expect(result.current.searchValue).toBe('');
    expect(result.current.page).toBe(1);
    expect(result.current.appliedQuery.term).toBe('');
  });

  it('resets page to 1 on filter change', () => {
    const { result } = renderHook(() => useEntityList());

    act(() => {
      result.current.setPage(4);
    });
    act(() => {
      result.current.handleFilterChange('active');
    });

    expect(result.current.page).toBe(1);
    expect(result.current.activeFilter).toBe('active');
  });

  it('updates view mode', () => {
    const { result } = renderHook(() => useEntityList());

    act(() => {
      result.current.setViewMode('thumbnail');
    });
    expect(result.current.viewMode).toBe('thumbnail');

    act(() => {
      result.current.setViewMode('list');
    });
    expect(result.current.viewMode).toBe('list');
  });

  it('toggles sort order', () => {
    const { result } = renderHook(() => useEntityList());

    act(() => {
      result.current.setSortOrder('desc');
    });
    expect(result.current.sortOrder).toBe('desc');

    act(() => {
      result.current.setSortOrder('asc');
    });
    expect(result.current.sortOrder).toBe('asc');
  });
});
