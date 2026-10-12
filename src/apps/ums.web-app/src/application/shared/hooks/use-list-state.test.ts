import { describe, it, expect } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useListState } from './use-list-state';

describe('useListState', () => {
  it('returns default pagination and query state', () => {
    const { result } = renderHook(() => useListState());

    expect(result.current.paginationState.page).toBe(1);
    expect(result.current.paginationState.pageSize).toBe(10);
    expect(result.current.paginationState.startIndex).toBe(0);
    expect(result.current.queryState.searchCriteria).toBe('code');
    expect(result.current.queryState.activeFilter).toBe('all');
    expect(result.current.queryState.sortBy).toBe('code');
  });

  it('respects custom initial options', () => {
    const { result } = renderHook(() =>
      useListState({
        initialPage: 3,
        initialPageSize: 25,
        defaultCriteria: 'name',
        defaultFilter: 'Active' as unknown as string,
      })
    );

    expect(result.current.paginationState.page).toBe(3);
    expect(result.current.paginationState.pageSize).toBe(25);
    expect(result.current.queryState.searchCriteria).toBe('name');
    expect(result.current.queryState.activeFilter).toBe('Active');
  });

  it('updates page via setPage', () => {
    const { result } = renderHook(() => useListState());

    act(() => {
      result.current.paginationState.setPage(5);
    });

    expect(result.current.paginationState.page).toBe(5);
  });

  it('updates pageSize and resets page to 1', () => {
    const { result } = renderHook(() =>
      useListState({
        initialPage: 3,
      })
    );

    act(() => {
      result.current.paginationState.setPageSize(25);
    });

    expect(result.current.paginationState.pageSize).toBe(25);
    expect(result.current.paginationState.page).toBe(1);
  });

  it('updates searchValue via queryState', () => {
    const { result } = renderHook(() => useListState());

    act(() => {
      result.current.queryState.setSearchValue('test search');
    });

    expect(result.current.queryState.searchValue).toBe('test search');
  });

  it('updates searchCriteria via queryState', () => {
    const { result } = renderHook(() => useListState());

    act(() => {
      result.current.queryState.setSearchCriteria('name');
    });

    expect(result.current.queryState.searchCriteria).toBe('name');
  });

  it('handles query submit', () => {
    const { result } = renderHook(() => useListState());

    act(() => {
      result.current.queryState.setSearchValue('test');
    });

    act(() => {
      result.current.queryState.handleQuerySubmit({ preventDefault: () => {} } as React.FormEvent);
    });

    expect(result.current.queryState.appliedQuery.term).toBe('test');
    expect(result.current.queryState.appliedQuery.filterApplied).toBe(true);
  });

  it('handles reset query', () => {
    const { result } = renderHook(() => useListState());

    act(() => {
      result.current.queryState.setSearchValue('test');
    });
    act(() => {
      result.current.queryState.handleQuerySubmit({ preventDefault: () => {} } as React.FormEvent);
    });

    act(() => {
      result.current.queryState.handleResetQuery();
    });

    expect(result.current.queryState.searchValue).toBe('');
    expect(result.current.queryState.appliedQuery.term).toBe('');
    expect(result.current.queryState.appliedQuery.filterApplied).toBe(false);
  });

  it('toggles sort order', () => {
    const { result } = renderHook(() => useListState());

    expect(result.current.queryState.sortOrder).toBe('asc');

    act(() => {
      result.current.queryState.toggleSortOrder();
    });

    expect(result.current.queryState.sortOrder).toBe('desc');

    act(() => {
      result.current.queryState.toggleSortOrder();
    });

    expect(result.current.queryState.sortOrder).toBe('asc');
  });

  it('calculates correct startIndex', () => {
    const { result } = renderHook(() =>
      useListState({
        initialPage: 3,
        initialPageSize: 20,
      })
    );

    expect(result.current.paginationState.startIndex).toBe(40);
  });
});
