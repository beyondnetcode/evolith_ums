import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useFormFields } from './use-form-fields';

describe('useFormFields', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('initializes with default values', () => {
    const { result } = renderHook(() =>
      useFormFields({ name: 'John', email: 'john@example.com', age: 25 })
    );

    expect(result.current.fields).toEqual({
      name: 'John',
      email: 'john@example.com',
      age: 25,
    });
  });

  it('sets a single field value', () => {
    const { result } = renderHook(() => useFormFields({ name: 'John', email: 'john@example.com' }));

    act(() => {
      result.current.setField('name', 'Jane');
    });

    expect(result.current.fields.name).toBe('Jane');
    expect(result.current.fields.email).toBe('john@example.com');
  });

  it('sets multiple fields at once', () => {
    const { result } = renderHook(() =>
      useFormFields({ name: 'John', email: 'john@example.com', age: 25 })
    );

    act(() => {
      result.current.setFields({ name: 'Jane', age: 30 });
    });

    expect(result.current.fields.name).toBe('Jane');
    expect(result.current.fields.age).toBe(30);
    expect(result.current.fields.email).toBe('john@example.com');
  });

  it('resets fields to defaults', () => {
    const { result } = renderHook(() => useFormFields({ name: 'John', email: 'john@example.com' }));

    act(() => {
      result.current.setField('name', 'Jane');
    });
    expect(result.current.fields.name).toBe('Jane');

    act(() => {
      result.current.resetFields();
    });

    expect(result.current.fields.name).toBe('John');
  });

  it('resets fields to custom values', () => {
    const { result } = renderHook(() => useFormFields({ name: 'John', email: 'john@example.com' }));

    act(() => {
      result.current.resetFields({ name: 'Custom', email: 'custom@test.com' });
    });

    expect(result.current.fields).toEqual({
      name: 'Custom',
      email: 'custom@test.com',
    });
  });

  it('uses ref to track latest defaults on reset', () => {
    const { result, rerender } = renderHook(({ defaults }) => useFormFields(defaults), {
      initialProps: { defaults: { name: 'Initial' } },
    });

    act(() => {
      result.current.setField('name', 'Modified');
    });
    expect(result.current.fields.name).toBe('Modified');

    rerender({ defaults: { name: 'Updated' } });

    act(() => {
      result.current.resetFields();
    });

    expect(result.current.fields.name).toBe('Updated');
  });
});
