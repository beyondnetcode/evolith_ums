import { usePaginationState, PAGE_SIZE_OPTIONS, type PageSizeOption } from './use-pagination-state';
import { useQueryState } from './use-query-state';

/**
 * Options for configuring the list state hook.
 * @template TFilter - The type of filter values (e.g., 'all' | 'Active' | 'Inactive')
 * @template TSort - The type of sort field values (e.g., 'code' | 'name' | 'status')
 */
export interface UseListStateOptions<TFilter extends string, TSort extends string> {
  /** Initial page number (default: 1) */
  initialPage?: number;
  /** Initial page size (default: 10) */
  initialPageSize?: PageSizeOption;
  /** Default search criteria field (default: 'code') */
  defaultCriteria?: string;
  /** Default active filter value (default: 'all') */
  defaultFilter?: TFilter;
  /** Default sort field (default: 'code') */
  defaultSortBy?: TSort;
}

/**
 * Composable hook that combines pagination and query state management for list views.
 * Provides a unified interface for managing page/size selection alongside search, filter, and sort controls.
 *
 * @example
 * ```tsx
 * const { paginationState, queryState } = useListState({
 *   initialPageSize: 25,
 *   defaultCriteria: 'name',
 *   defaultFilter: 'Active',
 * });
 * ```
 */
export function useListState<TFilter extends string = string, TSort extends string = string>(
  options: UseListStateOptions<TFilter, TSort> = {}
) {
  const {
    initialPage = 1,
    initialPageSize = 10,
    defaultCriteria = 'code',
    defaultFilter = 'all' as unknown as TFilter,
    defaultSortBy = 'code' as unknown as TSort,
  } = options;

  const paginationState = usePaginationState({
    initialPage,
    initialPageSize,
  });

  const queryState = useQueryState<TFilter, TSort>({
    criteria: defaultCriteria,
    filter: defaultFilter,
    sortBy: defaultSortBy,
  });

  return {
    paginationState,
    queryState,
  };
}

export { PAGE_SIZE_OPTIONS };
export type { PageSizeOption };
