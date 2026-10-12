import { describe, it, expect } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useCascadingSelect } from './use-cascading-select';

describe('useCascadingSelect', () => {
  it('initializes with empty values', () => {
    const { result } = renderHook(() => useCascadingSelect(['parent', 'child'] as const));

    expect(result.current.values.parent).toBe('');
    expect(result.current.values.child).toBe('');
  });

  it('accepts initial values', () => {
    const { result } = renderHook(() =>
      useCascadingSelect(['parent', 'child'] as const, { parent: 'initial' })
    );

    expect(result.current.values.parent).toBe('initial');
    expect(result.current.values.child).toBe('');
  });

  it('sets value for specific key', () => {
    const { result } = renderHook(() => useCascadingSelect(['parent', 'child'] as const));

    act(() => {
      result.current.handlers.setValue('parent', 'new-value');
    });

    expect(result.current.values.parent).toBe('new-value');
    expect(result.current.values.child).toBe('');
  });

  it('resets all values to initial', () => {
    const { result } = renderHook(() =>
      useCascadingSelect(['parent', 'child'] as const, { parent: 'init' })
    );

    act(() => {
      result.current.handlers.setValue('parent', 'changed');
      result.current.handlers.setValue('child', 'also-changed');
    });

    expect(result.current.values.parent).toBe('changed');
    expect(result.current.values.child).toBe('also-changed');

    act(() => {
      result.current.handlers.reset();
    });

    expect(result.current.values.parent).toBe('init');
    expect(result.current.values.child).toBe('');
  });

  it('resets values from a specific key onwards', () => {
    const { result } = renderHook(() =>
      useCascadingSelect(['grandparent', 'parent', 'child'] as const, {
        grandparent: 'gp-val',
        parent: 'p-val',
        child: 'c-val',
      })
    );

    act(() => {
      result.current.handlers.resetFrom('parent');
    });

    expect(result.current.values.grandparent).toBe('gp-val');
    expect(result.current.values.parent).toBe('');
    expect(result.current.values.child).toBe('');
  });

  it('handles non-existent key in resetFrom gracefully', () => {
    const { result } = renderHook(() => useCascadingSelect(['parent', 'child'] as const));

    act(() => {
      result.current.handlers.setValue('parent', 'test');
      result.current.handlers.setValue('child', 'test2');
    });

    act(() => {
      result.current.handlers.resetFrom('nonexistent' as never);
    });

    expect(result.current.values.parent).toBe('test');
    expect(result.current.values.child).toBe('test2');
  });

  it('supports multiple cascading levels', () => {
    const keys = ['level1', 'level2', 'level3', 'level4'] as const;
    const { result } = renderHook(() => useCascadingSelect(keys));

    act(() => {
      result.current.handlers.setValue('level1', 'a');
    });
    act(() => {
      result.current.handlers.setValue('level2', 'b');
    });
    act(() => {
      result.current.handlers.setValue('level3', 'c');
    });
    act(() => {
      result.current.handlers.setValue('level4', 'd');
    });

    expect(Object.values(result.current.values)).toEqual(['a', 'b', 'c', 'd']);
  });
});
