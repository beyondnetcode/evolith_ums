/**
 * useLocalOverrides<T> — Optimistic local patch layer over server data.
 *
 * Merges server-returned items with local patches so that UI changes
 * are reflected immediately without waiting for a refetch.
 *
 * H-5: Added rollback support for optimistic update failures.
 * H-7: Added getDiffs to inspect locally modified items.
 */
import { useState, useMemo, useCallback, useRef } from 'react';

export interface UseLocalOverridesReturn<T> {
  items: T[];
  patchItem: (id: string, patch: Partial<T>) => void;
  patchItems: (updater: (current: T[]) => T[]) => void;
  clearOverrides: () => void;
  rollbackItem: (id: string) => void;
  rollbackAll: () => void;
  getDiffs: () => { id: string; original: T; current: T }[];
  isDirty: (id: string) => boolean;
}

export function useLocalOverrides<T extends Record<string, unknown>>(
  serverItems: T[] | undefined,
  idKey: keyof T & string
): UseLocalOverridesReturn<T> {
  const [overrides, setOverrides] = useState<Record<string, Partial<T>>>({});
  const snapshotRef = useRef<Record<string, T>>({});

  const items = useMemo(() => {
    const base = serverItems ?? [];
    if (Object.keys(overrides).length === 0) return base;
    return base.map(item => {
      const id = String(item[idKey]);
      const patch = overrides[id];
      if (!patch) return item;
      if (!snapshotRef.current[id]) {
        snapshotRef.current[id] = { ...item };
      }
      return { ...item, ...patch };
    });
  }, [serverItems, overrides, idKey]);

  const patchItem = (id: string, patch: Partial<T>) => {
    setOverrides(prev => {
      if (!snapshotRef.current[id] && serverItems) {
        const original = serverItems.find(item => String(item[idKey]) === id);
        if (original) snapshotRef.current[id] = { ...original };
      }
      return { ...prev, [id]: { ...(prev[id] ?? {}), ...patch } };
    });
  };

  const patchItems = (updater: (current: T[]) => T[]) => {
    const next = updater(items);
    const newOverrides: Record<string, Partial<T>> = {};
    next.forEach(item => {
      const id = String(item[idKey]);
      if (!snapshotRef.current[id] && serverItems) {
        const original = serverItems.find(i => String(i[idKey]) === id);
        if (original) snapshotRef.current[id] = { ...original };
      }
      newOverrides[id] = item;
    });
    setOverrides(prev => ({ ...prev, ...newOverrides }));
  };

  const clearOverrides = () => {
    setOverrides({});
    snapshotRef.current = {};
  };

  const rollbackItem = (id: string) => {
    setOverrides(prev => {
      const next = { ...prev };
      delete next[id];
      return next;
    });
    delete snapshotRef.current[id];
  };

  const rollbackAll = () => clearOverrides();

  const getDiffs = useCallback(() => {
    if (!serverItems) return [];
    return Object.entries(overrides)
      .map(([id, patch]) => {
        const original = snapshotRef.current[id];
        if (!original) return null;
        const current = { ...original, ...patch } as T;
        return { id, original, current };
      })
      .filter((d): d is { id: string; original: T; current: T } => d !== null);
  }, [overrides, serverItems]);

  const isDirty = (id: string) => id in overrides;

  return {
    items,
    patchItem,
    patchItems,
    clearOverrides,
    rollbackItem,
    rollbackAll,
    getDiffs,
    isDirty,
  };
}
