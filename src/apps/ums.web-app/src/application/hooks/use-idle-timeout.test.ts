import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useIdleTimeout } from './use-idle-timeout';

describe('useIdleTimeout', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('returns resetTimer function', () => {
    const { result } = renderHook(() =>
      useIdleTimeout({
        timeoutMs: 1000,
        onIdle: () => {},
        enabled: true,
      })
    );

    expect(result.current.resetTimer).toBeDefined();
    expect(typeof result.current.resetTimer).toBe('function');
  });

  it('does not set timers when disabled', () => {
    const onIdle = vi.fn();
    const setTimeoutSpy = vi.spyOn(window, 'setTimeout');

    renderHook(() =>
      useIdleTimeout({
        timeoutMs: 1000,
        onIdle,
        enabled: false,
      })
    );

    expect(setTimeoutSpy).not.toHaveBeenCalled();
  });

  it('calls onIdle after timeout', () => {
    const onIdle = vi.fn();

    renderHook(() =>
      useIdleTimeout({
        timeoutMs: 1000,
        onIdle,
        enabled: true,
      })
    );

    vi.advanceTimersByTime(1000);
    expect(onIdle).toHaveBeenCalled();
  });

  it('calls onWarning before idle', () => {
    const onIdle = vi.fn();
    const onWarning = vi.fn();

    renderHook(() =>
      useIdleTimeout({
        timeoutMs: 1000,
        onIdle,
        onWarning,
        enabled: true,
      })
    );

    vi.advanceTimersByTime(400);
    expect(onWarning).toHaveBeenCalled();
    expect(onIdle).not.toHaveBeenCalled();
  });

  it('resets timer when resetTimer is called', () => {
    const onIdle = vi.fn();

    const { result } = renderHook(() =>
      useIdleTimeout({
        timeoutMs: 1000,
        onIdle,
        enabled: true,
      })
    );

    vi.advanceTimersByTime(500);
    result.current.resetTimer();
    vi.advanceTimersByTime(500);

    expect(onIdle).not.toHaveBeenCalled();

    vi.advanceTimersByTime(500);
    expect(onIdle).toHaveBeenCalled();
  });
});
