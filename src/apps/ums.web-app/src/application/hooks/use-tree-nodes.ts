/**
 * useTreeNodes.ts — Generic hook for building parent→child tree structures.
 *
 * Accepts a flat array and a parentId key, returns grouped tree nodes
 * with expand/collapse state management.
 */
import { useState, useMemo, useCallback } from 'react';

export interface TreeNode<T> {
  item: T;
  children: T[];
  isParent: boolean;
}

interface UseTreeNodesOptions<T> {
  items: T[];
  parentIdKey: keyof T;
  idKey: keyof T;
}

interface UseTreeNodesReturn<T> {
  treeNodes: TreeNode<T>[];
  expandedIds: Set<string>;
  toggleExpand: (id: string) => void;
  isExpanded: (id: string) => boolean;
  hasChildren: (id: string) => boolean;
  getChildren: (id: string) => T[];
}

export function useTreeNodes<T extends Record<string, unknown>>({
  items,
  parentIdKey,
  idKey,
}: UseTreeNodesOptions<T>): UseTreeNodesReturn<T> {
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());

  const toggleExpand = useCallback((id: string) => {
    setExpandedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const isExpanded = useCallback((id: string) => expandedIds.has(id), [expandedIds]);

  const treeNodes = useMemo((): TreeNode<T>[] => {
    const childMap = new Map<string, T[]>();
    const roots: T[] = [];

    items.forEach(item => {
      const parentId = item[parentIdKey] as string | null | undefined;
      if (parentId) {
        if (!childMap.has(parentId)) childMap.set(parentId, []);
        const children = childMap.get(parentId);
        if (children) children.push(item);
      } else {
        roots.push(item);
      }
    });

    return roots.map(root => {
      const id = String(root[idKey]);
      return {
        item: root,
        children: childMap.get(id) ?? [],
        isParent: true,
      };
    });
  }, [items, parentIdKey, idKey]);

  const hasChildren = useCallback(
    (id: string) => {
      const node = treeNodes.find(n => String(n.item[idKey]) === id);
      return node ? node.children.length > 0 : false;
    },
    [treeNodes, idKey]
  );

  const getChildren = useCallback(
    (id: string) => {
      const node = treeNodes.find(n => String(n.item[idKey]) === id);
      return node?.children ?? [];
    },
    [treeNodes, idKey]
  );

  return { treeNodes, expandedIds, toggleExpand, isExpanded, hasChildren, getChildren };
}
