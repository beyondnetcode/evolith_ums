import { useState, useCallback } from 'react';

/**
 * Type representing cascading select values as a record.
 * @template T - The union of select field keys
 */
export type CascadingValues<T extends string> = Record<T, string>;

/**
 * Result interface for useCascadingSelect hook.
 * @template T - The union of select field keys
 */
export interface UseCascadingSelectResult<T extends string> {
  /** Current values for all select fields */
  values: CascadingValues<T>;
  /** Handler functions for value management */
  handlers: {
    /** Set a specific field's value */
    setValue: (key: T, value: string) => void;
    /** Reset all fields to initial values */
    reset: () => void;
    /** Reset all fields from a given key onwards (clears dependent selects) */
    resetFrom: (fromKey: T) => void;
  };
}

/**
 * Manages state for cascading select dropdowns where selecting a parent
 * clears dependent child selections.
 *
 * @param keys - Array of field keys in dependency order (parent first)
 * @param initialValues - Optional initial values for the fields
 * @returns Object containing current values and handler functions
 *
 * @example
 * ```tsx
 * const { values, handlers } = useCascadingSelect(
 *   ['systemSuite', 'module', 'parameter'] as const,
 *   { systemSuite: 'ss-1' }
 * );
 *
 * // When user changes systemSuite, call resetFrom('systemSuite')
 * // to clear module and parameter selections
 * handlers.setValue('systemSuite', 'ss-2');
 * handlers.resetFrom('systemSuite');
 * ```
 */
export function useCascadingSelect<T extends string>(
  keys: T[],
  initialValues?: Partial<CascadingValues<T>>
): UseCascadingSelectResult<T> {
  const initial: CascadingValues<T> = keys.reduce((acc, key) => {
    acc[key] = initialValues?.[key] ?? '';
    return acc;
  }, {} as CascadingValues<T>);

  const [values, setValues] = useState<CascadingValues<T>>(initial);

  const setValue = useCallback((key: T, value: string) => {
    setValues(prev => ({ ...prev, [key]: value }));
  }, []);

  const reset = useCallback(() => {
    setValues(initial);
  }, [initial]);

  const resetFrom = useCallback(
    (fromKey: T) => {
      const keyIndex = keys.indexOf(fromKey);
      if (keyIndex === -1) return;
      setValues(prev => {
        const next = { ...prev };
        for (let i = keyIndex; i < keys.length; i++) {
          next[keys[i]] = '';
        }
        return next;
      });
    },
    [keys]
  );

  return { values, handlers: { setValue, reset, resetFrom } };
}
