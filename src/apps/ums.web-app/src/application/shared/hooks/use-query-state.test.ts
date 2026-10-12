import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useQueryState } from './use-query-state';

describe('useQueryState', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('initializes with provided values', () => {
    const { result } = renderHook(() =>
      useQueryState({ criteria: 'name', filter: 'all', sortBy: 'name' })
    );

    expect(result.current.searchCriteria).toBe('name');
    expect(result.current.searchValue).toBe('');
    expect(result.current.activeFilter).toBe('all');
    expect(result.current.sortBy).toBe('name');
    expect(result.current.sortOrder).toBe('asc');
  });

  it('sets search value', () => {
    const { result } = renderHook(() =>
      useQueryState({ criteria: 'name', filter: 'all', sortBy: 'name' })
    );

    act(() => {
      result.current.setSearchValue('test');
    });

    expect(result.current.searchValue).toBe('test');
  });

  it('sets active filter', () => {
    const { result } = renderHook(() =>
      useQueryState({ criteria: 'name', filter: 'all', sortBy: 'name' })
    );

    act(() => {
      result.current.setActiveFilter('active');
    });

    expect(result.current.activeFilter).toBe('active');
  });

  it('sets sort by', () => {
    const { result } = renderHook(() =>
      useQueryState({ criteria: 'name', filter: 'all', sortBy: 'name' })
    );

    act(() => {
      result.current.setSortBy('date');
    });

    expect(result.current.sortBy).toBe('date');
  });

  it('toggles sort order', () => {
    const { result } = renderHook(() =>
      useQueryState({ criteria: 'name', filter: 'all', sortBy: 'name' })
    );

    expect(result.current.sortOrder).toBe('asc');

    act(() => {
      result.current.toggleSortOrder();
    });

    expect(result.current.sortOrder).toBe('desc');

    act(() => {
      result.current.toggleSortOrder();
    });

    expect(result.current.sortOrder).toBe('asc');
  });

  it('submits query and updates appliedQuery', () => {
    const { result } = renderHook(() =>
      useQueryState({ criteria: 'name', filter: 'all', sortBy: 'name' })
    );

    act(() => {
      result.current.setSearchValue('search term');
    });

    const mockEvent = { preventDefault: vi.fn() } as unknown as React.FormEvent;
    act(() => {
      result.current.handleQuerySubmit(mockEvent);
    });

    expect(mockEvent.preventDefault).toHaveBeenCalled();
    expect(result.current.appliedQuery.criteria).toBe('name');
    expect(result.current.appliedQuery.term).toBe('search term');
  });

  it('trims search value on submit', () => {
    const { result } = renderHook(() =>
      useQueryState({ criteria: 'email', filter: 'all', sortBy: 'name' })
    );

    act(() => {
      result.current.setSearchValue('  trimmed  ');
    });

    const mockEvent = { preventDefault: vi.fn() } as unknown as React.FormEvent;
    act(() => {
      result.current.handleQuerySubmit(mockEvent);
    });

    expect(result.current.appliedQuery.term).toBe('trimmed');
  });

  it('resets query to initial state', () => {
    const { result } = renderHook(() =>
      useQueryState({ criteria: 'name', filter: 'all', sortBy: 'name' })
    );

    act(() => {
      result.current.setSearchValue('test');
      result.current.setSearchCriteria('email');
    });

    const mockEvent = { preventDefault: vi.fn() } as unknown as React.FormEvent;
    act(() => {
      result.current.handleQuerySubmit(mockEvent);
    });

    expect(result.current.appliedQuery.term).toBe('test');

    act(() => {
      result.current.handleResetQuery();
    });

    expect(result.current.searchValue).toBe('');
    expect(result.current.appliedQuery.term).toBe('');
    expect(result.current.appliedQuery.criteria).toBe('name');
  });
});
