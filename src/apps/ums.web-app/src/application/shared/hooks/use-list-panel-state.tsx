import { useMemo } from 'react';
import { RequiresFilterPrompt, PaginationFooter } from '@shared/components';
import type { QueryState, PaginationState } from '@domain/common/types/query-params';

/**
 * Configuration options for a list panel.
 * @template T - The type of items in the list
 */
export interface ListPanelOptions<T> {
  /** List of items to display */
  items: T[];
  /** ID of the currently selected item */
  selectedId: string;
  /** Whether data is currently loading */
  isLoading: boolean;
  /** Error object if the last request failed */
  error: Error | null;
  /** Current view mode (list or thumbnail) */
  viewMode: 'list' | 'thumbnail';
  /** Callback when view mode changes */
  onViewModeChange: (mode: 'list' | 'thumbnail') => void;
  /** Query state from useQueryState */
  queryState: QueryState;
  /** Pagination state with totals */
  paginationState: PaginationState & { totalItems: number; totalPages: number };
  /** Callback to register a new item */
  onRegisterNew: () => void;
  /** Whether to show filter prompt when no filter applied */
  requiresFilter?: boolean;
  /** Title for the filter prompt */
  filterPromptTitle?: string;
  /** Message for the filter prompt */
  filterPromptMessage?: string;
  /** Label for items (e.g., 'users', 'configurations') */
  itemLabel?: string;
  /** Render function for list view */
  renderList: () => React.ReactNode;
  /** Render function for thumbnail view */
  renderThumbnail: () => React.ReactNode;
}

/**
 * Composable hook that provides pagination and filter prompt rendering for list panels.
 * Combines pagination state and query state to produce display-ready components.
 *
 * @example
 * ```tsx
 * const panelState = useListPanelState({
 *   items,
 *   selectedId,
 *   isLoading,
 *   error,
 *   viewMode,
 *   onViewModeChange,
 *   queryState,
 *   paginationState,
 *   onRegisterNew,
 *   requiresFilter: true,
 *   itemLabel: 'usuarios',
 *   renderList: () => <ListView />,
 *   renderThumbnail: () => <GridView />,
 * });
 * ```
 */
export function useListPanelState<T>(options: ListPanelOptions<T>) {
  const {
    items,
    selectedId,
    isLoading,
    error,
    viewMode,
    onViewModeChange,
    queryState,
    paginationState,
    onRegisterNew,
    requiresFilter = false,
    filterPromptTitle = 'Aplica un filtro para cargar datos',
    filterPromptMessage = 'Selecciona un estado o ingresa un término de búsqueda para visualizar los elementos.',
    itemLabel = 'elementos',
  } = options;

  const pagination = useMemo(() => {
    if (paginationState.totalPages === 0) return undefined;
    return {
      page: paginationState.page,
      pageSize: paginationState.pageSize,
      totalItems: paginationState.totalItems,
      totalPages: paginationState.totalPages,
      onPageChange:
        (paginationState as { handlePageChange?: (p: number) => void }).handlePageChange ??
        paginationState.page,
      onPageSizeChange: (paginationState as { handlePageSizeChange?: (s: number) => void })
        .handlePageSizeChange,
    };
  }, [paginationState]);

  const totalItems = paginationState.totalItems;
  const startIndex = paginationState.startIndex ?? 0;
  const pageSize = paginationState.pageSize;

  const filterPrompt = requiresFilter ? (
    <RequiresFilterPrompt title={filterPromptTitle} message={filterPromptMessage} />
  ) : null;

  const footerTelemetry = (
    <PaginationFooter
      totalItems={totalItems}
      startIndex={startIndex}
      pageSize={pageSize}
      itemLabel={itemLabel}
      onClear={queryState.handleResetQuery}
      searchTerm={queryState.appliedQuery.term}
    />
  );

  return {
    pagination,
    totalItems,
    startIndex,
    pageSize,
    filterPrompt,
    footerTelemetry,
  };
}
