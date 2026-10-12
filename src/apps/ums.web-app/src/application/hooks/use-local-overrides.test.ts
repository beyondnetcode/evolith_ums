import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useLocalOverrides } from './use-local-overrides';

interface TestItem {
  id: string;
  name: string;
  value: number;
}

describe('useLocalOverrides', () => {
  const serverItems: TestItem[] = [
    { id: '1', name: 'Item 1', value: 100 },
    { id: '2', name: 'Item 2', value: 200 },
    { id: '3', name: 'Item 3', value: 300 },
  ];

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('returns server items when no overrides', () => {
    const { result } = renderHook(() => useLocalOverrides(serverItems, 'id'));

    expect(result.current.items).toEqual(serverItems);
  });

  it('returns empty array when serverItems is undefined', () => {
    const { result } = renderHook(() => useLocalOverrides<TestItem>(undefined, 'id'));

    expect(result.current.items).toEqual([]);
  });

  it('patches a single item', () => {
    const { result } = renderHook(() => useLocalOverrides(serverItems, 'id'));

    act(() => {
      result.current.patchItem('1', { name: 'Updated Item 1' });
    });

    expect(result.current.items[0].name).toBe('Updated Item 1');
    expect(result.current.items[1].name).toBe('Item 2');
  });

  it('tracks dirty state', () => {
    const { result } = renderHook(() => useLocalOverrides(serverItems, 'id'));

    expect(result.current.isDirty('1')).toBe(false);

    act(() => {
      result.current.patchItem('1', { name: 'Updated' });
    });

    expect(result.current.isDirty('1')).toBe(true);
    expect(result.current.isDirty('2')).toBe(false);
  });

  it('clears all overrides', () => {
    const { result } = renderHook(() => useLocalOverrides(serverItems, 'id'));

    act(() => {
      result.current.patchItem('1', { name: 'Updated' });
      result.current.patchItem('2', { value: 999 });
    });

    expect(result.current.isDirty('1')).toBe(true);

    act(() => {
      result.current.clearOverrides();
    });

    expect(result.current.isDirty('1')).toBe(false);
    expect(result.current.items).toEqual(serverItems);
  });

  it('rolls back a single item', () => {
    const { result } = renderHook(() => useLocalOverrides(serverItems, 'id'));

    act(() => {
      result.current.patchItem('1', { name: 'Updated' });
      result.current.patchItem('2', { value: 999 });
    });

    act(() => {
      result.current.rollbackItem('1');
    });

    expect(result.current.isDirty('1')).toBe(false);
    expect(result.current.isDirty('2')).toBe(true);
    expect(result.current.items[0].name).toBe('Item 1');
  });

  it('rolls back all items', () => {
    const { result } = renderHook(() => useLocalOverrides(serverItems, 'id'));

    act(() => {
      result.current.patchItem('1', { name: 'Updated' });
    });

    act(() => {
      result.current.rollbackAll();
    });

    expect(result.current.isDirty('1')).toBe(false);
  });

  it('returns diffs for modified items', () => {
    const { result } = renderHook(() => useLocalOverrides(serverItems, 'id'));

    act(() => {
      result.current.patchItem('1', { name: 'Updated Item 1' });
    });

    const diffs = result.current.getDiffs();

    expect(diffs).toHaveLength(1);
    expect(diffs[0].id).toBe('1');
    expect(diffs[0].original.name).toBe('Item 1');
    expect(diffs[0].current.name).toBe('Updated Item 1');
  });

  it('returns empty diffs when no server items', () => {
    const { result } = renderHook(() => useLocalOverrides<TestItem>(undefined, 'id'));

    expect(result.current.getDiffs()).toEqual([]);
  });

  it('patches multiple items via updater function', () => {
    const { result } = renderHook(() => useLocalOverrides(serverItems, 'id'));

    act(() => {
      result.current.patchItems(items => items.map(item => ({ ...item, value: item.value * 2 })));
    });

    expect(result.current.items[0].value).toBe(200);
    expect(result.current.items[1].value).toBe(400);
    expect(result.current.items[2].value).toBe(600);
  });
});
