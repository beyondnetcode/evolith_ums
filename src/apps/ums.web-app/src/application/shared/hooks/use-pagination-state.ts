import { useState, useCallback, useMemo } from 'react';

export const PAGE_SIZE_OPTIONS = [10, 25, 50] as const;
export type PageSizeOption = (typeof PAGE_SIZE_OPTIONS)[number];

export interface UsePaginationStateOptions {
  initialPage?: number;
  initialPageSize?: PageSizeOption;
}

export function usePaginationState(options: UsePaginationStateOptions = {}) {
  const [page, setPage] = useState<number>(options.initialPage ?? 1);
  const [pageSize, setPageSize] = useState<PageSizeOption>(options.initialPageSize ?? 10);

  const startIndex = useMemo(() => (page - 1) * pageSize, [page, pageSize]);

  const handlePageChange = useCallback((newPage: number) => {
    setPage(newPage);
  }, []);

  const handlePageSizeChange = useCallback((newPageSize: PageSizeOption) => {
    setPageSize(newPageSize);
    setPage(1);
  }, []);

  return {
    page,
    setPage,
    pageSize,
    setPageSize: handlePageSizeChange,
    startIndex,
    handlePageChange,
    handlePageSizeChange,
  };
}
