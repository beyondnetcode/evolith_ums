import { useEffect, useRef, useCallback } from 'react';

const DEFAULT_IDLE_TIMEOUT_MS = 15 * 60 * 1000;
const WARNING_BEFORE_MS = 60 * 1000;

interface UseIdleTimeoutOptions {
  timeoutMs?: number;
  onIdle: () => void;
  onWarning?: () => void;
  enabled: boolean;
}

export function useIdleTimeout({
  timeoutMs = DEFAULT_IDLE_TIMEOUT_MS,
  onIdle,
  onWarning,
  enabled,
}: UseIdleTimeoutOptions) {
  const timeoutRef = useRef<ReturnType<typeof setTimeout>>();
  const warningRef = useRef<ReturnType<typeof setTimeout>>();

  const resetTimer = useCallback(() => {
    if (!enabled) return;

    if (timeoutRef.current) clearTimeout(timeoutRef.current);
    if (warningRef.current) clearTimeout(warningRef.current);

    warningRef.current = setTimeout(() => {
      onWarning?.();
    }, timeoutMs - WARNING_BEFORE_MS);

    timeoutRef.current = setTimeout(() => {
      onIdle();
    }, timeoutMs);
  }, [enabled, timeoutMs, onIdle, onWarning]);

  useEffect(() => {
    if (!enabled) return;

    const events = ['mousedown', 'keydown', 'scroll', 'touchstart', 'mousemove'];
    const handler = resetTimer;

    events.forEach(event => window.addEventListener(event, handler, { passive: true }));
    resetTimer();

    return () => {
      events.forEach(event => window.removeEventListener(event, handler));
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
      if (warningRef.current) clearTimeout(warningRef.current);
    };
  }, [enabled, resetTimer]);

  return { resetTimer };
}
