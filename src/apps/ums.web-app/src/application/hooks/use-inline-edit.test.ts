import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useInlineEdit } from './use-inline-edit';

interface TestEntity {
  id: string;
  name: string;
  email: string;
  age: number;
}

describe('useInlineEdit', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('initializes with no editing', () => {
    const { result } = renderHook(() => useInlineEdit<TestEntity>());

    expect(result.current.editingId).toBeNull();
    expect(result.current.hasEditing).toBe(false);
    expect(result.current.draft).toEqual({});
  });

  it('opens edit with entity data', () => {
    const { result } = renderHook(() => useInlineEdit<TestEntity>());
    const entity: TestEntity = { id: '1', name: 'John', email: 'john@test.com', age: 30 };

    act(() => {
      result.current.openEdit('1', entity);
    });

    expect(result.current.editingId).toBe('1');
    expect(result.current.hasEditing).toBe(true);
    expect(result.current.draft.name).toBe('John');
    expect(result.current.draft.email).toBe('john@test.com');
  });

  it('only picks specified fields', () => {
    const { result } = renderHook(() => useInlineEdit<TestEntity>(['name', 'email']));
    const entity: TestEntity = { id: '1', name: 'John', email: 'john@test.com', age: 30 };

    act(() => {
      result.current.openEdit('1', entity);
    });

    expect(result.current.draft.name).toBe('John');
    expect(result.current.draft.email).toBe('john@test.com');
    expect(result.current.draft.age).toBeUndefined();
  });

  it('checks if specific id is editing', () => {
    const { result } = renderHook(() => useInlineEdit<TestEntity>());
    const entity: TestEntity = { id: '1', name: 'John', email: 'john@test.com', age: 30 };

    act(() => {
      result.current.openEdit('1', entity);
    });

    expect(result.current.isEditing('1')).toBe(true);
    expect(result.current.isEditing('2')).toBe(false);
  });

  it('sets draft field', () => {
    const { result } = renderHook(() => useInlineEdit<TestEntity>());
    const entity: TestEntity = { id: '1', name: 'John', email: 'john@test.com', age: 30 };

    act(() => {
      result.current.openEdit('1', entity);
      result.current.setField('name', 'Jane');
    });

    expect(result.current.draft.name).toBe('Jane');
  });

  it('cancels edit', () => {
    const { result } = renderHook(() => useInlineEdit<TestEntity>());
    const entity: TestEntity = { id: '1', name: 'John', email: 'john@test.com', age: 30 };

    act(() => {
      result.current.openEdit('1', entity);
    });
    expect(result.current.hasEditing).toBe(true);

    act(() => {
      result.current.cancelEdit();
    });

    expect(result.current.editingId).toBeNull();
    expect(result.current.hasEditing).toBe(false);
    expect(result.current.draft).toEqual({});
  });

  it('commits edit and returns draft', () => {
    const { result } = renderHook(() => useInlineEdit<TestEntity>());
    const entity: TestEntity = { id: '1', name: 'John', email: 'john@test.com', age: 30 };

    act(() => {
      result.current.openEdit('1', entity);
      result.current.setField('name', 'Jane');
    });

    let commitResult: ReturnType<typeof result.current.commitEdit> = null;
    act(() => {
      commitResult = result.current.commitEdit();
    });

    expect(commitResult?.id).toBe('1');
    expect(commitResult?.draft.name).toBe('Jane');
    expect(commitResult?.draft.email).toBe('john@test.com');
    expect(result.current.editingId).toBeNull();
  });

  it('returns null when committing without editing', () => {
    const { result } = renderHook(() => useInlineEdit<TestEntity>());

    let commitResult: ReturnType<typeof result.current.commitEdit>;
    act(() => {
      commitResult = result.current.commitEdit();
    });

    expect(commitResult).toBeNull();
  });
});
