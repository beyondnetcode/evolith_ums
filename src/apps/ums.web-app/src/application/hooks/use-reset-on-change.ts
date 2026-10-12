/**
 * useResetOnChange — Runs a reset callback whenever a watched key changes.
 *
 * M-12: Fixed eslint-disable by using ref to track latest resetFn.
 */
import { useEffect, useRef } from 'react';

export function useResetOnChange(key: string | undefined, resetFn: () => void): void {
  const prevKey = useRef(key);
  const resetFnRef = useRef(resetFn);

  useEffect(() => {
    resetFnRef.current = resetFn;
  }, [resetFn]);

  useEffect(() => {
    if (prevKey.current !== key) {
      prevKey.current = key;
      resetFnRef.current();
    }
  }, [key]);
}
