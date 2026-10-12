import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useFormValidation } from './use-form-validation';
import { z } from 'zod';

describe('useFormValidation', () => {
  const schema = z.object({
    name: z.string().min(1, 'Name is required'),
    email: z.string().email('Invalid email'),
    age: z.number().min(18, 'Must be 18 or older'),
  });

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('returns empty errors initially', () => {
    const { result } = renderHook(() => useFormValidation(schema));
    expect(result.current.errors).toEqual({});
  });

  it('validates correct data without errors', () => {
    const { result } = renderHook(() => useFormValidation(schema));

    const data = result.current.validate({
      name: 'John',
      email: 'john@example.com',
      age: 25,
    });

    expect(data).toEqual({
      name: 'John',
      email: 'john@example.com',
      age: 25,
    });
    expect(result.current.errors).toEqual({});
  });

  it('returns null for invalid data', () => {
    const { result } = renderHook(() => useFormValidation(schema));

    const data = result.current.validate({
      name: '',
      email: 'invalid',
      age: 10,
    });

    expect(data).toBeNull();
  });

  it('clears errors when clearErrors is called', () => {
    const { result } = renderHook(() => useFormValidation(schema));

    act(() => {
      result.current.validate({ name: '', email: 'invalid', age: 10 });
    });

    act(() => {
      result.current.clearErrors();
    });

    expect(result.current.errors).toEqual({});
  });

  it('can set errors directly via setErrors', () => {
    const { result } = renderHook(() => useFormValidation(schema));

    act(() => {
      result.current.setErrors({ name: 'Custom error' });
    });

    expect(result.current.errors).toEqual({ name: 'Custom error' });
  });
});
