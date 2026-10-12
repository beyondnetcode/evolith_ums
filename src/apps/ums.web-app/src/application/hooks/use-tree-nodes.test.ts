import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useTreeNodes } from './use-tree-nodes';

interface TreeNode {
  id: string;
  name: string;
  parentId: string | null;
}

describe('useTreeNodes', () => {
  const flatItems: TreeNode[] = [
    { id: '1', name: 'Root 1', parentId: null },
    { id: '2', name: 'Root 2', parentId: null },
    { id: '3', name: 'Child of 1', parentId: '1' },
    { id: '4', name: 'Child of 1', parentId: '1' },
    { id: '5', name: 'Grandchild of 1', parentId: '3' },
    { id: '6', name: 'Child of 2', parentId: '2' },
  ];

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('builds tree nodes from flat array', () => {
    const { result } = renderHook(() =>
      useTreeNodes({ items: flatItems, parentIdKey: 'parentId', idKey: 'id' })
    );

    expect(result.current.treeNodes).toHaveLength(2);
    expect(result.current.treeNodes[0].item.name).toBe('Root 1');
    expect(result.current.treeNodes[1].item.name).toBe('Root 2');
  });

  it('identifies parent nodes', () => {
    const { result } = renderHook(() =>
      useTreeNodes({ items: flatItems, parentIdKey: 'parentId', idKey: 'id' })
    );

    expect(result.current.treeNodes[0].isParent).toBe(true);
  });

  it('returns children for a parent node', () => {
    const { result } = renderHook(() =>
      useTreeNodes({ items: flatItems, parentIdKey: 'parentId', idKey: 'id' })
    );

    const children = result.current.getChildren('1');
    expect(children).toHaveLength(2);
    expect(children[0].name).toBe('Child of 1');
  });

  it('checks if a root node has children', () => {
    const { result } = renderHook(() =>
      useTreeNodes({ items: flatItems, parentIdKey: 'parentId', idKey: 'id' })
    );

    expect(result.current.hasChildren('1')).toBe(true);
    expect(result.current.hasChildren('2')).toBe(true);
  });

  it('toggles expand state', () => {
    const { result } = renderHook(() =>
      useTreeNodes({ items: flatItems, parentIdKey: 'parentId', idKey: 'id' })
    );

    expect(result.current.isExpanded('1')).toBe(false);

    act(() => {
      result.current.toggleExpand('1');
    });

    expect(result.current.isExpanded('1')).toBe(true);

    act(() => {
      result.current.toggleExpand('1');
    });

    expect(result.current.isExpanded('1')).toBe(false);
  });

  it('handles empty items array', () => {
    const { result } = renderHook(() =>
      useTreeNodes({ items: [], parentIdKey: 'parentId', idKey: 'id' })
    );

    expect(result.current.treeNodes).toHaveLength(0);
  });

  it('returns empty children for non-existent id', () => {
    const { result } = renderHook(() =>
      useTreeNodes({ items: flatItems, parentIdKey: 'parentId', idKey: 'id' })
    );

    expect(result.current.getChildren('nonexistent')).toEqual([]);
  });
});
