import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useResetOnChange } from './use-reset-on-change';

describe('useResetOnChange', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('calls resetFn when key changes from initial value', () => {
    const resetFn = vi.fn();
    const { rerender } = renderHook(({ key, resetFn }) => useResetOnChange(key, resetFn), {
      initialProps: { key: 'initial', resetFn },
    });

    rerender({ key: 'changed', resetFn });

    expect(resetFn).toHaveBeenCalledTimes(1);
  });

  it('does not call resetFn when key stays the same', () => {
    const resetFn = vi.fn();
    const { rerender } = renderHook(({ key, resetFn }) => useResetOnChange(key, resetFn), {
      initialProps: { key: 'same', resetFn },
    });

    rerender({ key: 'same', resetFn });

    expect(resetFn).not.toHaveBeenCalled();
  });

  it('calls resetFn when key changes from undefined to a value', () => {
    const resetFn = vi.fn();
    const { rerender } = renderHook(({ key, resetFn }) => useResetOnChange(key, resetFn), {
      initialProps: { key: undefined, resetFn },
    });

    rerender({ key: 'new-key', resetFn });

    expect(resetFn).toHaveBeenCalledTimes(1);
  });

  it('uses latest resetFn via ref', () => {
    const resetFn1 = vi.fn();
    const resetFn2 = vi.fn();

    const { rerender } = renderHook(({ key, resetFn }) => useResetOnChange(key, resetFn), {
      initialProps: { key: 'initial', resetFn: resetFn1 },
    });

    rerender({ key: 'changed', resetFn: resetFn2 });

    expect(resetFn1).not.toHaveBeenCalled();
    expect(resetFn2).toHaveBeenCalledTimes(1);
  });
});
