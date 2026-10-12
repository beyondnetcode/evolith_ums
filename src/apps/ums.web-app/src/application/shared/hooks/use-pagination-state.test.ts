import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { usePaginationState } from './use-pagination-state';

describe('usePaginationState', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('initializes with default values', () => {
    const { result } = renderHook(() => usePaginationState());

    expect(result.current.page).toBe(1);
    expect(result.current.pageSize).toBe(10);
    expect(result.current.startIndex).toBe(0);
  });

  it('accepts custom initial values', () => {
    const { result } = renderHook(() =>
      usePaginationState({ initialPage: 3, initialPageSize: 25 })
    );

    expect(result.current.page).toBe(3);
    expect(result.current.pageSize).toBe(25);
    expect(result.current.startIndex).toBe(50);
  });

  it('calculates correct startIndex', () => {
    const { result } = renderHook(() =>
      usePaginationState({ initialPage: 2, initialPageSize: 15 })
    );

    expect(result.current.startIndex).toBe(15);
  });

  it('changes page', () => {
    const { result } = renderHook(() => usePaginationState());

    act(() => {
      result.current.handlePageChange(5);
    });

    expect(result.current.page).toBe(5);
    expect(result.current.startIndex).toBe(40);
  });

  it('resets page when page size changes', () => {
    const { result } = renderHook(() => usePaginationState());

    act(() => {
      result.current.handlePageChange(5);
    });
    expect(result.current.page).toBe(5);

    act(() => {
      result.current.handlePageSizeChange(25);
    });

    expect(result.current.pageSize).toBe(25);
    expect(result.current.page).toBe(1);
  });

  it('sets page directly', () => {
    const { result } = renderHook(() => usePaginationState());

    act(() => {
      result.current.setPage(10);
    });

    expect(result.current.page).toBe(10);
  });

  it('resets page when setPageSize is used', () => {
    const { result } = renderHook(() => usePaginationState());

    act(() => {
      result.current.setPage(5);
      result.current.setPageSize(50);
    });

    expect(result.current.pageSize).toBe(50);
    expect(result.current.page).toBe(1);
  });
});
