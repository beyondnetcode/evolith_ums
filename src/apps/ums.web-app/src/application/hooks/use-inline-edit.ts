/**
 * useInlineEdit<T> — Generic inline-edit state manager
 *
 * Encapsulates the repeated pattern of:
 *   1. Track which entity is being edited (by id)
 *   2. Copy entity fields into local state on open
 *   3. Update individual fields
 *   4. Cancel → reset
 *   5. Save → return draft, reset
 *
 * Generic over entity shape T; the consumer declares which fields
 * are editable via the `fields` pick list.
 */
import { useState, useCallback, useRef, useEffect } from 'react';

export interface UseInlineEditReturn<T> {
  /** ID of the entity currently being edited, or null. */
  editingId: string | null;
  /** Current draft values (empty object when not editing). */
  draft: Partial<T>;
  /** True if the given id matches the currently-editing entity. */
  isEditing: (id: string) => boolean;
  /** True if *any* entity is being edited. */
  hasEditing: boolean;
  /** Begin editing — copies the specified fields from `entity` into draft. */
  openEdit: (id: string, entity: T) => void;
  /** Discard the current edit. */
  cancelEdit: () => void;
  /** Update a single draft field. */
  setField: <K extends keyof T>(key: K, value: T[K]) => void;
  /**
   * Finalise the edit — returns the current draft and resets state.
   * Returns `null` if not currently editing.
   */
  commitEdit: () => { id: string; draft: Partial<T> } | null;
}

/**
 * @param fields - The subset of keys to copy from the entity when editing begins.
 *                 If omitted, all enumerable keys are copied.
 */
export function useInlineEdit<T extends Record<string, unknown>>(
  fields?: (keyof T)[]
): UseInlineEditReturn<T> {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draft, setDraft] = useState<Partial<T>>({});
  const fieldsRef = useRef(fields);

  useEffect(() => {
    fieldsRef.current = fields;
  }, [fields]);

  const openEdit = useCallback((id: string, entity: T) => {
    const picked: Partial<T> = {};
    const keys = (fieldsRef.current ?? Object.keys(entity)) as (keyof T)[];
    for (const k of keys) {
      picked[k] = entity[k];
    }
    setEditingId(id);
    setDraft(picked);
  }, []);

  const cancelEdit = useCallback(() => {
    setEditingId(null);
    setDraft({});
  }, []);

  const setField = useCallback(<K extends keyof T>(key: K, value: T[K]) => {
    setDraft(prev => ({ ...prev, [key]: value }));
  }, []);

  const commitEdit = useCallback(() => {
    if (!editingId) return null;
    const result = { id: editingId, draft: { ...draft } };
    setEditingId(null);
    setDraft({});
    return result;
  }, [editingId, draft]);

  const isEditing = useCallback((id: string) => editingId === id, [editingId]);

  return {
    editingId,
    draft,
    isEditing,
    hasEditing: editingId !== null,
    openEdit,
    cancelEdit,
    setField,
    commitEdit,
  };
}
