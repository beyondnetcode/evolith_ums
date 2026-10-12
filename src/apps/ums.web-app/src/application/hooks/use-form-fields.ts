/**
 * useFormFields<T> — Generic form state manager for controlled forms.
 *
 * H-6: Fixed stale closure bug — uses ref to track latest defaults.
 */
import { useState, useCallback, useRef, useEffect } from 'react';

export interface UseFormFieldsReturn<T extends Record<string, unknown>> {
  fields: T;
  setField: <K extends keyof T>(key: K, value: T[K]) => void;
  setFields: (patch: Partial<T>) => void;
  resetFields: (values?: T) => void;
}

export function useFormFields<T extends Record<string, unknown>>(
  defaults: T
): UseFormFieldsReturn<T> {
  const [fields, setFieldsState] = useState<T>(defaults);
  const defaultsRef = useRef(defaults);

  useEffect(() => {
    defaultsRef.current = defaults;
  }, [defaults]);

  const setField = useCallback(<K extends keyof T>(key: K, value: T[K]) => {
    setFieldsState(prev => ({ ...prev, [key]: value }));
  }, []);

  const setFields = useCallback((patch: Partial<T>) => {
    setFieldsState(prev => ({ ...prev, ...patch }));
  }, []);

  const resetFields = useCallback((values?: T) => {
    setFieldsState(values ?? defaultsRef.current);
  }, []);

  return { fields, setField, setFields, resetFields };
}
