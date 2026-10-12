import { useSyncExternalStore } from 'react';

/**
 * use-breakpoint — hooks de viewport SSR-safe basados en `matchMedia`.
 *
 * Se apoyan en `useSyncExternalStore` para evitar parpadeos de hidratación y
 * re-render innecesario. Los breakpoints se alinean con Tailwind:
 *   sm 640 · md 768 · lg 1024 · xl 1280 · 2xl 1536
 */

const noopUnsubscribe = () => {};

function subscribe(query: string) {
  return (callback: () => void) => {
    if (typeof window === 'undefined' || !window.matchMedia) return noopUnsubscribe;
    const mql = window.matchMedia(query);
    mql.addEventListener('change', callback);
    return () => mql.removeEventListener('change', callback);
  };
}

function getSnapshot(query: string) {
  return () => {
    if (typeof window === 'undefined' || !window.matchMedia) return false;
    return window.matchMedia(query).matches;
  };
}

/** Devuelve `true` mientras la media query coincida; reactivo a cambios de tamaño/orientación. */
export function useMediaQuery(query: string): boolean {
  return useSyncExternalStore(subscribe(query), getSnapshot(query), () => false);
}

/** `true` bajo el breakpoint `lg` de Tailwind (< 1024px): teléfonos y tablets pequeñas. */
export function useIsMobile(): boolean {
  return useMediaQuery('(max-width: 1023px)');
}

/** `true` entre `md` y `lg` (768–1023px): tablets en vertical. */
export function useIsTablet(): boolean {
  return useMediaQuery('(min-width: 768px) and (max-width: 1023px)');
}
