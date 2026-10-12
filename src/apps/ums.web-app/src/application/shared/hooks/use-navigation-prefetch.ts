/**
 * useNavigationPrefetch.ts
 *
 * React Query hook for prefetching screen data on hover to improve
 * perceived navigation performance.
 *
 * Uses debouncing to avoid redundant prefetch calls when users
 * quickly move between navigation items.
 *
 * Usage:
 *   const { prefetchById } = useNavigationPrefetch();
 *   <button onMouseOver={() => prefetchById('tenants')} />
 */
import { useCallback, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { NavItemId } from '@shared/layouts/navigation.config';
import { NAV_PREFETCH_CONFIG } from '@app/shared/config/navigation-prefetch.config';

const PREFETCH_DEBOUNCE_MS = 150;

export function useNavigationPrefetch() {
  let queryClient: ReturnType<typeof useQueryClient> | null = null;
  try {
    queryClient = useQueryClient();
  } catch {
    queryClient = null;
  }
  const prefetchTimers = useRef<Map<NavItemId, ReturnType<typeof setTimeout>>>(new Map());

  const prefetchById = useCallback(
    async (screenId: NavItemId) => {
      const existing = prefetchTimers.current.get(screenId);
      if (existing) return;

      const timer = setTimeout(async () => {
        prefetchTimers.current.delete(screenId);
        const entry = NAV_PREFETCH_CONFIG[screenId];
        if (!entry) return;

        await queryClient?.prefetchQuery({
          queryKey: entry.queryKey,
          queryFn: entry.queryFn,
          staleTime: entry.staleTime ?? 30_000,
        });
      }, PREFETCH_DEBOUNCE_MS);

      prefetchTimers.current.set(screenId, timer);
    },
    [queryClient]
  );

  return { prefetchById };
}
