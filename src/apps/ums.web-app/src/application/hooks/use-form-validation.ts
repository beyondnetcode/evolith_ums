import { useState, useCallback, useMemo, useEffect } from 'react';
import { z } from 'zod';

export type FieldErrors = Record<string, string | undefined>;

export interface UseFormValidationOptions<T> {
  schema: z.ZodType<T>;
  onSubmit?: (data: T) => void | Promise<void>;
  onError?: (errors: FieldErrors) => void;
  validateOnChange?: boolean;
  validateOnBlur?: boolean;
}

export interface UseFormValidationReturn<T> {
  errors: FieldErrors;
  setErrors: React.Dispatch<React.SetStateAction<FieldErrors>>;
  fieldErrors: FieldErrors;
  touchedFields: Set<string>;
  isDirty: boolean;
  isValid: boolean;
  isSubmitting: boolean;
  validate: (data: unknown) => T | null;
  validateField: (fieldName: string, value: unknown) => boolean;
  clearErrors: () => void;
  clearFieldError: (fieldName: string) => void;
  setFieldTouched: (fieldName: string) => void;
  resetForm: (initialValues?: Partial<T>) => void;
  handleSubmit: (e: React.FormEvent) => Promise<void>;
  getFieldProps: (fieldName: string) => {
    error: string | undefined;
    onChange: (value: unknown) => void;
    onBlur: () => void;
    value: unknown;
  };
}

export function useFormValidation<T extends z.ZodTypeAny>(
  schema: T,
  options?: UseFormValidationOptions<z.infer<T>>
): UseFormValidationReturn<z.infer<T>> {
  const { onSubmit, onError, validateOnChange = false, validateOnBlur = true } = options || {};

  const [errors, setErrors] = useState<FieldErrors>({});
  const [touchedFields, setTouchedFields] = useState<Set<string>>(new Set());
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [initialValues, setInitialValues] = useState<Record<string, unknown>>({});

  const validate = useCallback(
    (data: unknown): z.infer<T> | null => {
      setErrors({});
      const result = schema.safeParse(data);

      if (!result.success) {
        const fieldErrors: FieldErrors = {};
        const flattened = result.error.flatten();

        for (const [key, messages] of Object.entries(flattened.fieldErrors)) {
          if (messages && messages.length > 0) {
            fieldErrors[key] = messages[0];
          }
        }

        setErrors(fieldErrors);
        onError?.(fieldErrors);
        return null;
      }

      return result.data;
    },
    [schema, onError]
  );

  const validateField = useCallback(
    (fieldName: string, value: unknown): boolean => {
      const partialData = { ...initialValues, [fieldName]: value };

      try {
        const fieldSchema = schema.shape?.[fieldName];
        if (!fieldSchema) return true;

        const result = fieldSchema.safeParse(value);

        if (!result.success) {
          setErrors(prev => ({
            ...prev,
            [fieldName]: result.error.errors[0]?.message || 'Invalid value',
          }));
          return false;
        }

        setErrors(prev => {
          const newErrors = { ...prev };
          delete newErrors[fieldName];
          return newErrors;
        });
        return true;
      } catch {
        return true;
      }
    },
    [schema, initialValues]
  );

  const clearErrors = useCallback(() => {
    setErrors({});
  }, []);

  const clearFieldError = useCallback((fieldName: string) => {
    setErrors(prev => {
      const newErrors = { ...prev };
      delete newErrors[fieldName];
      return newErrors;
    });
  }, []);

  const setFieldTouched = useCallback((fieldName: string) => {
    setTouchedFields(prev => new Set(prev).add(fieldName));
  }, []);

  const resetForm = useCallback((newInitialValues?: Partial<z.infer<T>>) => {
    if (newInitialValues) {
      setInitialValues(newInitialValues as Record<string, unknown>);
    }
    setErrors({});
    setTouchedFields(new Set());
  }, []);

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();

      const form = e.currentTarget as HTMLFormElement;
      const formData = new FormData(form);
      const data: Record<string, unknown> = {};

      for (const [key, value] of formData.entries()) {
        data[key] = value;
      }

      setIsSubmitting(true);

      const validated = validate(data);

      if (validated) {
        try {
          await onSubmit?.(validated);
        } catch (error) {
          console.error('Form submission error:', error);
        }
      }

      setIsSubmitting(false);
    },
    [validate, onSubmit]
  );

  const getFieldProps = useCallback(
    (fieldName: string) => ({
      error: errors[fieldName],
      onChange: (value: unknown) => {
        if (validateOnChange) {
          validateField(fieldName, value);
        }
      },
      onBlur: () => {
        if (validateOnBlur) {
          const input = document.querySelector(`[name="${fieldName}"]`) as HTMLInputElement;
          if (input) {
            validateField(fieldName, input.value);
          }
        }
        setFieldTouched(fieldName);
      },
      value: initialValues[fieldName] ?? '',
    }),
    [errors, validateOnChange, validateOnBlur, validateField, setFieldTouched, initialValues]
  );

  const fieldErrors = useMemo(() => {
    const result: FieldErrors = {};
    for (const [key, value] of Object.entries(errors)) {
      if (touchedFields.has(key)) {
        result[key] = value;
      }
    }
    return result;
  }, [errors, touchedFields]);

  const isDirty = useMemo(() => {
    return Object.keys(errors).length > 0 || touchedFields.size > 0;
  }, [errors, touchedFields]);

  const isValid = Object.keys(errors).length === 0;

  return {
    errors,
    setErrors,
    fieldErrors,
    touchedFields,
    isDirty,
    isValid,
    isSubmitting,
    validate,
    validateField,
    clearErrors,
    clearFieldError,
    setFieldTouched,
    resetForm,
    handleSubmit,
    getFieldProps,
  };
}

export function useFormDirty<T extends Record<string, unknown>>(
  initialValues: T,
  currentValues: T
): boolean {
  return useMemo(() => {
    return Object.keys(initialValues).some(key => initialValues[key] !== currentValues[key]);
  }, [initialValues, currentValues]);
}

export function useFieldError(
  errors: FieldErrors,
  fieldName: string,
  touchedFields: Set<string>
): string | undefined {
  return useMemo(() => {
    if (touchedFields.has(fieldName)) {
      return errors[fieldName];
    }
    return undefined;
  }, [errors, fieldName, touchedFields]);
}
