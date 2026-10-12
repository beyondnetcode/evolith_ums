import { useMemo } from 'react';
import {
  STATUS_COLORS,
  STATUS_LABELS_ES,
  type StatusColorConfig,
} from '@shared/utils/status-utils';

/**
 * Configuration interface for list panel item extraction.
 * @template T - The type of items in the list
 */
export interface ListPanelConfig<T> {
  /** Function to extract the unique ID from an item */
  getItemId: (item: T) => string;
  /** Function to extract the display label from an item */
  getItemLabel: (item: T) => string;
  /** Function to extract the code from an item */
  getItemCode: (item: T) => string;
  /** Function to extract the status from an item */
  getItemStatus: (item: T) => string;
  /** Optional function to extract a description from an item */
  getItemDescription?: (item: T) => string | undefined;
  /** Optional custom status color mapping */
  statusColorMap?: Record<string, StatusColorConfig>;
}

/**
 * Provides status color and label utilities for list panels.
 * Uses shared STATUS_COLORS and STATUS_LABELS_ES from status-utils.
 *
 * @param config - List panel configuration with status extraction functions
 * @returns Object with getStatusColors and getStatusLabel functions
 */
export function useStatusConfig<T>(config: ListPanelConfig<T>) {
  const getStatusColors = useMemo(() => {
    return (status: string): StatusColorConfig => {
      return (
        config.statusColorMap?.[status] ??
        STATUS_COLORS[status as keyof typeof STATUS_COLORS] ?? {
          bg: 'bg-m3-surface-variant',
          border: 'border-m3-outline/20',
          text: 'text-m3-secondary',
        }
      );
    };
  }, [config.statusColorMap]);

  const getStatusLabel = useMemo(() => {
    return (status: string): string => {
      return STATUS_LABELS_ES[status] ?? status;
    };
  }, []);

  return { getStatusColors, getStatusLabel };
}

/**
 * Creates pagination config object for DataViewShell from pagination state.
 * Returns undefined when totalPages is 0 (no data to paginate).
 *
 * @param paginationState - Current pagination state with totals
 * @param setPage - Function to update current page
 * @param setPageSize - Function to update page size
 * @returns Pagination config for DataViewShell or undefined
 */
export function createPaginationConfig(
  paginationState: {
    page: number;
    pageSize: number;
    totalItems: number;
    totalPages: number;
    startIndex?: number;
  },
  setPage: (page: number) => void,
  setPageSize: (size: number) => void
) {
  return paginationState.totalPages > 0
    ? {
        page: paginationState.page,
        pageSize: paginationState.pageSize,
        totalItems: paginationState.totalItems,
        totalPages: paginationState.totalPages,
        onPageChange: setPage,
        onPageSizeChange: setPageSize,
      }
    : undefined;
}
